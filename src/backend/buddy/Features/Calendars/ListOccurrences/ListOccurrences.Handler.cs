using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListOccurrencesHandler
{
    // Keeps a single request's expansion work bounded regardless of how many recurring items a
    // calendar has.
    public const int MaxRangeDays = 366;

    public static async Task<ListOccurrencesResult> Handle(
        ListOccurrences query,
        IUserEventStore users,
        ICalendarEventStore calendars,
        ICalendarItemEventStore items,
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

        var userId = await users.FindUserIdAsync(query.Subject, cancellationToken);

        if (userId is null)
        {
            return new ListOccurrencesResult([], CalendarAccess.NotFound);
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = CalendarAuthorization.CheckView(calendar, userId);

        if (access != CalendarAccess.Allowed)
        {
            return new ListOccurrencesResult([], access);
        }

        var itemIds = await items.ListIdsForCalendarAsync(query.CalendarId, cancellationToken);
        var occurrences = new List<CalendarItemOccurrence>();

        foreach (var itemId in itemIds)
        {
            var itemEvents = await items.ReadAsync(itemId, cancellationToken);

            if (CalendarItem.Rehydrate(itemEvents) is not { IsDeleted: false } item)
            {
                continue;
            }

            if (item.Kind == CalendarItemKind.Event)
            {
                AddEventOccurrences(item, calendar!.TimeZoneId, query.From, query.To, occurrences);
            }
            else
            {
                AddTaskOccurrences(item, calendar!.TimeZoneId, query.From, query.To, occurrences);
            }
        }

        occurrences.Sort((a, b) => (a.StartsAt ?? a.DueAt)!.Value.CompareTo((b.StartsAt ?? b.DueAt)!.Value));

        return new ListOccurrencesResult(occurrences, CalendarAccess.Allowed);
    }

    private static void AddEventOccurrences(CalendarItem item, TimeZoneId zoneId, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var period = item.Period!;
        var duration = period.EndsAt.Date.ToDateTime(period.EndsAt.Time) - period.StartsAt.Date.ToDateTime(period.StartsAt.Time);

        foreach (var date in RecurrenceExpansion.ExpandDates(period.StartsAt.Date, item.Recurrence, from, to))
        {
            var startLocal = date.ToDateTime(period.StartsAt.Time);
            var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
            var endsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal + duration);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon.Value, item.Color.Value,
                startsAt, endsAt, null, item.CreatedBy.Value, item.LastModifiedBy.Value));
        }
    }

    private static void AddTaskOccurrences(CalendarItem item, TimeZoneId zoneId, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = item.DueDate!;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var dueAt = TimeZoneResolution.ResolveInstant(zoneId, date.ToDateTime(due.Time));

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon.Value, item.Color.Value,
                null, null, dueAt, item.CreatedBy.Value, item.LastModifiedBy.Value));
        }
    }
}
