using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Calendars;

public static class TransferCalendarToGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        TransferCalendarToGroup command,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);

        if (calendar is null)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        // Two-sided consent, the same shape ShareMealPlanWithGroup/ShareMedicineWithGroup use:
        // the calendar's current owner and the destination group's management both have to agree.
        var targetGroup = Group.Rehydrate(await groups.ReadAsync(command.NewGroupId, cancellationToken));

        if (targetGroup is null)
        {
            return new Result<Unit>.NotFound();
        }

        var groupAccess = GroupAuthorization.CheckManage(targetGroup, userId);

        if (groupAccess != GroupAccess.Allowed)
        {
            return groupAccess.ToDeniedResult<Unit>();
        }

        if (calendar.GroupId == command.NewGroupId)
        {
            // Already there -- idempotent, same rationale as UnshareMealPlanFromGroupHandler's.
            return new Result<Unit>.Success(Unit.Value);
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new CalendarTransferredToGroup(command.CalendarId, command.NewGroupId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
