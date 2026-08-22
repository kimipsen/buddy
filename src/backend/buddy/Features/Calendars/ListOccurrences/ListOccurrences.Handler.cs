using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListOccurrencesHandler
{
    // Keeps a single request's expansion work bounded regardless of how many recurring items a
    // calendar has.
    public const int MaxRangeDays = 366;

    public static async Task<Result<IReadOnlyCollection<CalendarItemOccurrence>>> Handle(
        ListOccurrences query,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.Validation("'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxRangeDays)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.Validation($"The requested range cannot exceed {MaxRangeDays} days.");
        }

        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckView(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.NotFound();
        }

        var occurrences = await CalendarOccurrenceExpansion.ExpandAsync(query.CalendarId, calendar!.TimeZoneId, query.From, query.To, items, cancellationToken);

        return new Result<IReadOnlyCollection<CalendarItemOccurrence>>.Success(occurrences);
    }
}
