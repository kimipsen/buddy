using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

public static class AssignPickupHandler
{
    public static async Task<Result<PickupOccurrence>> Handle(
        AssignPickup command,
        IPickupScheduleEventStore pickups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<PickupOccurrence>.NotFound();
        }

        var access = await PickupAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<PickupOccurrence>();
        }

        if (ValidateFields(command) is { } fieldError)
        {
            return new Result<PickupOccurrence>.Validation(fieldError);
        }

        if (await ValidateRelationshipAsync(command, guardians, cancellationToken) is { } relationshipError)
        {
            return new Result<PickupOccurrence>.Validation(relationshipError);
        }

        var after = new PickupAssignment(
            command.Kind, command.GuardianId, command.SiblingChildId,
            command.PlaydateHostName, command.PlaydateLocation, command.PlaydateContactInfo,
            command.Time, userId, command.Notes);
        var now = DateTimeOffset.UtcNow;

        var scheduleId = await pickups.FindIdForChildAsync(command.ChildId, cancellationToken);

        if (scheduleId is null)
        {
            var newId = PickupScheduleId.New();

            await pickups.CreateAsync(
                newId,
                [
                    new PickupScheduleCreated(newId, command.ChildId, now),
                    new PickupAssigned(newId, command.Date, command.Slot, Before: null, after, now)
                ],
                cancellationToken);
        }
        else
        {
            var events = await pickups.ReadAsync(scheduleId, cancellationToken);
            var schedule = PickupSchedule.Rehydrate(events)!;
            var before = schedule.Assignments.GetValueOrDefault((command.Date, command.Slot));

            // Compares content only, not AssignedBy -- re-asserting the same arrangement (even by
            // a different guardian) shouldn't produce a no-op history entry, the same rule
            // AssignMealToSlot/SetDoseStatus already apply.
            var unchanged = before is not null && before with { AssignedBy = after.AssignedBy } == after;

            if (!unchanged)
            {
                await pickups.AppendAsync(scheduleId, [new PickupAssigned(scheduleId, command.Date, command.Slot, before, after, now)], cancellationToken);
            }
        }

        return new Result<PickupOccurrence>.Success(PickupOccurrence.FromAssignment(command.Date, command.Slot, after));
    }

    // Structural validation: does the request carry the fields its own Kind needs, and none it
    // doesn't. Relationship validation (is GuardianId actually a guardian, is SiblingChildId
    // actually a sibling) needs async lookups and happens separately below.
    private static string? ValidateFields(AssignPickup command) => command.Kind switch
    {
        PickupAssigneeKind.Guardian => command.GuardianId is null
            ? "A guardian assignee requires guardianId."
            : null,
        PickupAssigneeKind.SelfEscort => null,
        PickupAssigneeKind.Sibling => command.SiblingChildId is null
            ? "A sibling assignee requires siblingChildId."
            : null,
        PickupAssigneeKind.Playdate => string.IsNullOrWhiteSpace(command.PlaydateHostName)
            ? "A playdate assignee requires playdateHostName."
            : null,
        _ => throw new UnreachableException($"Unrecognized PickupAssigneeKind value: {command.Kind}."),
    };

    // Returns a validation message, or null if the assignee is acceptable. Deliberately a small
    // local check against IGuardianLinkEventStore rather than a dependency on Mealplans'
    // MealFamilyResolution -- see docs/backend/analysis/pickup-schedules.md#question-3.
    private static async Task<string?> ValidateRelationshipAsync(AssignPickup command, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        switch (command.Kind)
        {
            case PickupAssigneeKind.Guardian:
                var link = await guardians.FindActiveLinkAsync(command.ChildId, command.GuardianId!, cancellationToken);
                return link is null ? "guardianId is not an active guardian of this child." : null;

            case PickupAssigneeKind.Sibling:
                var siblingChildId = command.SiblingChildId!;

                if (siblingChildId == command.ChildId)
                {
                    return "A child cannot be their own sibling escort.";
                }

                return await IsSiblingAsync(command.ChildId, siblingChildId, guardians, cancellationToken)
                    ? null
                    : "siblingChildId does not share an active guardian with this child.";

            default:
                return null;
        }
    }

    private static async Task<bool> IsSiblingAsync(UserId childId, UserId otherChildId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var childGuardianIds = (await guardians.ListForChildAsync(childId, cancellationToken))
            .Select(link => link.GuardianId)
            .ToHashSet();

        var otherGuardianLinks = await guardians.ListForChildAsync(otherChildId, cancellationToken);

        return otherGuardianLinks.Any(link => childGuardianIds.Contains(link.GuardianId));
    }
}
