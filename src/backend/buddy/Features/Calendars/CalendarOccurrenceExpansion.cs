using buddy.Features.TaskLibrary;

namespace buddy.Features.Calendars;

// Shared by ListOccurrences and the ical feed -- expands every non-deleted item in a calendar
// into concrete occurrences within [from, to], resolved to actual instants via the calendar's
// time zone. Nothing here is persisted or cached; it's recomputed from current state every call.
public static class CalendarOccurrenceExpansion
{
    public static async Task<IReadOnlyCollection<CalendarItemOccurrence>> ExpandAsync(
        CalendarId calendarId,
        TimeZoneId zoneId,
        Icon calendarIcon,
        DateOnly from,
        DateOnly to,
        ICalendarItemEventStore items,
        ITaskTemplateEventStore templates,
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
                AddEventOccurrences(item, zoneId, calendarIcon, from, to, occurrences);
            }
            else if (item.TaskTemplateId is { } rawTemplateId)
            {
                // Loaded once per item, outside the per-date loop below -- a naive per-(item,date)
                // load would be needlessly expensive for a long-running daily/weekly routine.
                var templateEvents = await templates.ReadAsync(new TaskTemplateId(rawTemplateId), cancellationToken);
                var template = TaskTemplate.Rehydrate(templateEvents);

                // Missing (hard-deleted) template: emit nothing for this item rather than throwing
                // -- same "skip inconsistent state, don't crash a whole calendar view" convention
                // as the IsDeleted filter above. An archived template, by contrast, still expands
                // normally -- archiving only blocks *new* scheduling; an already-scheduled
                // recurring item keeps running until the guardian deletes it.
                if (template is not null)
                {
                    AddTemplateTaskOccurrences(item, template, zoneId, calendarIcon, from, to, occurrences);
                }
            }
            else
            {
                AddTaskOccurrences(item, zoneId, calendarIcon, from, to, occurrences);
            }
        }

        occurrences.Sort((a, b) => (a.StartsAt ?? a.DueAt)!.Value.CompareTo((b.StartsAt ?? b.DueAt)!.Value));

        return occurrences;
    }

    private static void AddEventOccurrences(CalendarItem item, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var period = item.Period!;
        var duration = period.EndsAt.Date.ToDateTime(period.EndsAt.Time) - period.StartsAt.Date.ToDateTime(period.StartsAt.Time);

        foreach (var date in RecurrenceExpansion.ExpandDates(period.StartsAt.Date, item.Recurrence, from, to))
        {
            var startLocal = date.ToDateTime(period.StartsAt.Time);
            var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
            var endsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal + duration);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                startsAt, endsAt, null, period.IsAllDay, IsCompleted: false, item.CreatedBy.Value, item.LastModifiedBy.Value, AssignedTo: null));
        }
    }

    private static void AddTaskOccurrences(CalendarItem item, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = item.DueDate!;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var dueAt = TimeZoneResolution.ResolveInstant(zoneId, date.ToDateTime(due.Time));
            var isCompleted = item.CompletionLog.GetValueOrDefault((date, (Guid?)null), false);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                null, null, dueAt, due.IsAllDay, isCompleted, item.CreatedBy.Value, item.LastModifiedBy.Value, item.AssignedTo?.Value));
        }
    }

    // One occurrence per (date, subtask): each subtask's wall-clock window is computed first
    // (due.Time + a cumulative TimeSpan offset, still local time), and only then resolved through
    // TimeZoneResolution -- never by resolving the anchor to a single UTC instant and adding
    // TimeSpans to that instant, which would compute the wrong wall-clock boundary for any subtask
    // starting after a DST transition mid-routine.
    private static void AddTemplateTaskOccurrences(
        CalendarItem item, TaskTemplate template, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = item.DueDate!;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var offset = TimeSpan.Zero;

            foreach (var subtask in template.Subtasks)
            {
                var startLocal = date.ToDateTime(due.Time) + offset;
                var endLocal = startLocal + subtask.Duration;

                var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
                var endsAt = TimeZoneResolution.ResolveInstant(zoneId, endLocal);
                var isCompleted = item.CompletionLog.GetValueOrDefault((date, subtask.Id.Value), false);

                occurrences.Add(new CalendarItemOccurrence(
                    item.Id, item.Kind, subtask.Title, subtask.Icon?.Value ?? item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                    startsAt, endsAt, startsAt, due.IsAllDay, isCompleted, item.CreatedBy.Value, item.LastModifiedBy.Value, item.AssignedTo?.Value,
                    ParentTitle: item.Title, SubtaskId: subtask.Id.Value));

                offset += subtask.Duration;
            }
        }
    }
}
