using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Pickups;

public static class AssignPickupHandler
{
    public static async Task<Result<PickupOccurrence>> Handle(
        AssignPickup command,
        IValidator<AssignPickup> validator,
        IPickupScheduleEventStore pickups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<PickupOccurrence>.Validation(problem);
        }

        var userId = command.UserId;

        var access = await PickupAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<PickupOccurrence>();
        }

        if (await ValidateRelationshipAsync(command, guardians, cancellationToken) is { } relationshipProblem)
        {
            return new Result<PickupOccurrence>.Validation(relationshipProblem);
        }

        var after = new PickupAssignment(command.Assignee, command.Time, userId, command.Notes);
        var now = DateTimeOffset.UtcNow;

        var scheduleId = await pickups.FindIdForChildAsync(command.ChildId, cancellationToken);

        if (scheduleId is null)
        {
            var newId = PickupScheduleId.New();

            await pickups.CreateAsync(
                newId,
                [
                    new PickupScheduleCreated(newId, command.ChildId, now),
                    new PickupAssigned(newId, command.Date, command.Slot, after, now)
                ],
                cancellationToken);
        }
        else
        {
            var events = await pickups.ReadAsync(scheduleId, cancellationToken);
            var schedule = PickupSchedule.Replay(events);
            var before = schedule.Assignments.GetValueOrDefault((command.Date, command.Slot));

            // Compares content only, not AssignedBy -- re-asserting the same arrangement (even by
            // a different guardian) shouldn't produce a no-op history entry, the same rule
            // AssignMealToSlot/SetDoseStatus already apply.
            var unchanged = before is not null && before with { AssignedBy = after.AssignedBy } == after;

            if (!unchanged)
            {
                await pickups.AppendAsync(scheduleId, [new PickupAssigned(scheduleId, command.Date, command.Slot, after, now)], cancellationToken);
            }
        }

        return new Result<PickupOccurrence>.Success(PickupOccurrence.FromAssignment(command.Date, command.Slot, after));
    }

    // Returns a validation message, or null if the assignee is acceptable. Deliberately a small
    // local check against IGuardianLinkEventStore rather than a dependency on Mealplans'
    // MealFamilyResolution -- see docs/backend/analysis/pickup-schedules.md#question-3.
    private static async Task<ValidationProblem?> ValidateRelationshipAsync(AssignPickup command, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        command.Assignee switch
        {
            PickupAssignee.Guardian guardian =>
                await guardians.FindActiveLinkAsync(command.ChildId, guardian.GuardianId, cancellationToken) is null
                    ? ValidationProblem.Of("guardianId is not an active guardian of this child.")
                    : null,
            PickupAssignee.Sibling sibling when sibling.SiblingChildId == command.ChildId =>
                ValidationProblem.Of("A child cannot be their own sibling escort."),
            PickupAssignee.Sibling sibling =>
                await IsSiblingAsync(command.ChildId, sibling.SiblingChildId, guardians, cancellationToken)
                    ? null
                    : ValidationProblem.Of("siblingChildId does not share an active guardian with this child."),
            PickupAssignee.SelfEscort or PickupAssignee.Playdate => null,
        };

    private static async Task<bool> IsSiblingAsync(UserId childId, UserId otherChildId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var childGuardianIds = (await guardians.ListForChildAsync(childId, cancellationToken))
            .Select(link => link.GuardianId)
            .ToHashSet();

        var otherGuardianLinks = await guardians.ListForChildAsync(otherChildId, cancellationToken);

        return otherGuardianLinks.Any(link => childGuardianIds.Contains(link.GuardianId));
    }
}
