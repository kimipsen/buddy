using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListOccurrencesHandler
{
    // Keeps a single request's expansion work bounded regardless of how many recurring items a
    // calendar has.
    public const int MaxRangeDays = 366;

    public static async Task<ListOccurrencesResult> Handle(
        ListOccurrences query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new ListOccurrencesResult([], CalendarAccess.Allowed, "'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxRangeDays)
        {
            return new ListOccurrencesResult([], CalendarAccess.Allowed, $"The requested range cannot exceed {MaxRangeDays} days.");
        }

        if (query.UserId is not { } userId)
        {
            return new ListOccurrencesResult([], CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new ListOccurrencesResult([], access);
        }

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(query.CalendarId, calendar!.TimeZoneId, query.From, query.To, items, cancellationToken);

        return new ListOccurrencesResult(occurrences, CalendarAccess.Allowed);
    }
}
