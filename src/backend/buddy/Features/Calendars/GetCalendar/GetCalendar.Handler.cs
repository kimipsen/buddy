using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class GetCalendarHandler
{
    public static async Task<GetCalendarResult> Handle(GetCalendar query, IUserEventStore users, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(query.Subject, cancellationToken);

        if (userId is null)
        {
            return new GetCalendarResult(null, CalendarAccess.NotFound);
        }

        var events = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = CalendarAuthorization.CheckView(calendar, userId);

        return new GetCalendarResult(access == CalendarAccess.Allowed ? calendar : null, access);
    }
}
