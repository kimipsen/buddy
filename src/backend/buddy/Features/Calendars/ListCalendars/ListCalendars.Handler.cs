using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListCalendarsHandler
{
    public static async Task<IReadOnlyCollection<CalendarMembershipDocument>> Handle(ListCalendars query, IUserEventStore users, ICalendarEventStore calendars, CancellationToken cancellationToken)
    {
        var userId = await users.FindUserIdAsync(query.Subject, cancellationToken);

        if (userId is null)
        {
            return [];
        }

        return await calendars.ListForUserAsync(userId, cancellationToken);
    }
}
