using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class RemoveMemberHandler
{
    public static async Task<CalendarAccess> Handle(RemoveMember command, IUserEventStore users, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(command.Subject, cancellationToken);

        if (userId is null)
        {
            return CalendarAccess.NotFound;
        }

        var events = await calendars.ReadAsync(command.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = CalendarAuthorization.CheckOwner(calendar, userId);

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
