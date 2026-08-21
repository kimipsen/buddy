using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RemoveMemberHandler
{
    public static async Task<CalendarAccess> Handle(RemoveMember command, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return CalendarAccess.NotFound;
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckOwner(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access;
        }

        if (command.MemberId == userId)
        {
            // The owner can't remove themselves -- deleting the calendar is the only way to end it.
            return CalendarAccess.Forbidden;
        }

        if (!calendar!.Members.ContainsKey(command.MemberId))
        {
            return CalendarAccess.Allowed;
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new MemberRoleRevoked(command.CalendarId, command.MemberId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return CalendarAccess.Allowed;
    }
}
