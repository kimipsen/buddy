using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class GetCalendarHandler
{
    public static async Task<GetCalendarResult> Handle(GetCalendar query, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new GetCalendarResult(null, CalendarAccess.NotFound);
        }

        var events = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(events);
        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        return new GetCalendarResult(access == CalendarAccess.Allowed ? calendar : null, access);
    }
}
