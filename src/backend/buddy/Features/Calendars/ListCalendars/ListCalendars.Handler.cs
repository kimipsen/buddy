using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListCalendarsHandler
{
    public static async Task<IReadOnlyCollection<CalendarMembershipDocument>> Handle(ListCalendars query, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return [];
        }

        return await calendars.ListForUserAsync(userId, cancellationToken);
    }
}
