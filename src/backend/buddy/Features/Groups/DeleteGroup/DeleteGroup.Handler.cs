using buddy.Common;
using buddy.Features.Calendars;

namespace buddy.Features.Groups;

public static class DeleteGroupHandler
{
    // Cascade is best-effort and sequential, not a single cross-store transaction -- Groups and
    // Calendars are separate Marten stores/schemas, same as Users and Calendars already are, and
    // this codebase has no cross-store transactions anywhere (deleting a user doesn't
    // transactionally cascade to their calendars either). Access-control safety doesn't depend on
    // atomicity here: CalendarAuthorization's resolution already treats a deleted/unresolvable
    // group as "no access" for everyone, so nobody retains access via this group in the window
    // between GroupDeleted and its calendars being individually marked deleted.
    public static async Task<Result<Unit>> Handle(DeleteGroup command, IGroupEventStore groups, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckOwner(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access == GroupAccess.Forbidden ? new Result<Unit>.Forbidden() : new Result<Unit>.NotFound();
        }

        await groups.AppendAsync(command.GroupId, [new GroupDeleted(command.GroupId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        var owned = await calendars.ListOwnedByGroupsAsync([command.GroupId], cancellationToken);

        foreach (var calendar in owned)
        {
            var calendarId = new CalendarId(calendar.Id);
            await calendars.AppendAsync(calendarId, [new CalendarDeleted(calendarId, userId, DateTimeOffset.UtcNow)], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
