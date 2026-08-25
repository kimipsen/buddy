namespace buddy.Features.Calendars;

// Shared by ListOccurrences and the ical feed -- expands every non-deleted item in a calendar
// into concrete occurrences within [from, to], resolved to actual instants via the calendar's
// time zone. Nothing here is persisted or cached; it's recomputed from current state every call.
public static class CalendarOccurrenceExpansion
{
    public static async Task<IReadOnlyCollection<CalendarItemOccurrence>> ExpandAsync(
        CalendarId calendarId,
        TimeZoneId zoneId,
        DateOnly from,
        DateOnly to,
        ICalendarItemEventStore items,
        CancellationToken cancellationToken)
    {
        var itemIds = await items.ListIdsForCalendarAsync(calendarId, cancellationToken);
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
                AddEventOccurrences(item, zoneId, from, to, occurrences);
            }
            else
            {
                AddTaskOccurrences(item, zoneId, from, to, occurrences);
            }
        }

        occurrences.Sort((a, b) => (a.StartsAt ?? a.DueAt)!.Value.CompareTo((b.StartsAt ?? b.DueAt)!.Value));

        return occurrences;
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
                startsAt, endsAt, null, IsCompleted: false, item.CreatedBy.Value, item.LastModifiedBy.Value));
        }
    }

    private static void AddTaskOccurrences(CalendarItem item, TimeZoneId zoneId, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = item.DueDate!;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var dueAt = TimeZoneResolution.ResolveInstant(zoneId, date.ToDateTime(due.Time));
            var isCompleted = item.CompletionLog.GetValueOrDefault(date, false);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon.Value, item.Color.Value,
                null, null, dueAt, isCompleted, item.CreatedBy.Value, item.LastModifiedBy.Value));
        }
    }
}
