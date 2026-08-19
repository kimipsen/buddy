using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class DeleteCalendarHandler
{
    public static async Task<CalendarAccess> Handle(DeleteCalendar command, IUserEventStore users, ICalendarEventStore calendars, CancellationToken cancellationToken)
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

        await calendars.AppendAsync(command.CalendarId, [new CalendarDeleted(command.CalendarId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return CalendarAccess.Allowed;
    }
}
