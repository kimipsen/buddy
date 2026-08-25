using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Pickups;

public static class ClearPickupHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearPickup command,
        IPickupScheduleEventStore pickups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await PickupAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != PickupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var scheduleId = await pickups.FindIdForChildAsync(command.ChildId, cancellationToken);

        // No schedule stream at all yet, or nothing assigned at this slot -- clearing is
        // idempotent, so a guardian double-tapping "clear" isn't an error, the same rule
        // ClearMealSlot uses.
        if (scheduleId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var events = await pickups.ReadAsync(scheduleId, cancellationToken);
        var schedule = PickupSchedule.Rehydrate(events)!;

        if (schedule.Assignments.GetValueOrDefault((command.Date, command.Slot)) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await pickups.AppendAsync(scheduleId, [new PickupCleared(scheduleId, command.Date, command.Slot, before, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
