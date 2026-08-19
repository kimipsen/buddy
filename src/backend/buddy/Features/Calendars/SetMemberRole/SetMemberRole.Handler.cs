using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class SetMemberRoleHandler
{
    public static async Task<CalendarAccess> Handle(SetMemberRole command, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (command.Role == CalendarRole.Owner)
        {
            // Ownership is assigned only at creation and never granted through this endpoint.
            return CalendarAccess.Forbidden;
        }

        if (command.UserId is not { } userId)
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
            // The owner's own role can't be changed through this endpoint either.
            return CalendarAccess.Forbidden;
        }

        await calendars.AppendAsync(
            command.CalendarId,
            [new MemberRoleGranted(command.CalendarId, command.MemberId, command.Role, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return CalendarAccess.Allowed;
    }
}
