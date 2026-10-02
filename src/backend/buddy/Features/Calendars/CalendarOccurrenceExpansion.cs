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

            switch (item.Schedule)
            {
                case ItemSchedule.Event @event:
                    AddEventOccurrences(item, @event.Period, zoneId, calendarIcon, from, to, occurrences);
                    break;

                case ItemSchedule.Task task when task.Source is TaskSource.FromTemplate fromTemplate:
                    // Loaded once per item, outside the per-date loop below -- a naive per-(item,date)
                    // load would be needlessly expensive for a long-running daily/weekly routine.
                    var templateEvents = await templates.ReadAsync(new TaskTemplateId(fromTemplate.TaskTemplateId), cancellationToken);
                    var template = TaskTemplate.Rehydrate(templateEvents);

                    // Missing (hard-deleted) template: emit nothing for this item rather than throwing
                    // -- same "skip inconsistent state, don't crash a whole calendar view" convention
                    // as the IsDeleted filter above. An archived template, by contrast, still expands
                    // normally -- archiving only blocks *new* scheduling; an already-scheduled
                    // recurring item keeps running until the guardian deletes it.
                    if (template is not null)
                    {
                        AddTemplateTaskOccurrences(item, task, template, zoneId, calendarIcon, from, to, occurrences);
                    }

                    break;

                case ItemSchedule.Task task:
                    AddTaskOccurrences(item, task, zoneId, calendarIcon, from, to, occurrences);
                    break;
            }
        }

        occurrences.Sort((a, b) => a.SortAt.CompareTo(b.SortAt));

        return occurrences;
    }

    private static void AddEventOccurrences(CalendarItem item, Period period, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var duration = period.EndsAt.Date.ToDateTime(period.EndsAt.Time) - period.StartsAt.Date.ToDateTime(period.StartsAt.Time);

        foreach (var date in RecurrenceExpansion.ExpandDates(period.StartsAt.Date, item.Recurrence, from, to))
        {
            var startLocal = date.ToDateTime(period.StartsAt.Time);
            var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
            var endsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal + duration);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                new OccurrenceTiming.Timed(startsAt, endsAt), period.IsAllDay, IsCompleted: false, item.CreatedBy.Value, item.LastModifiedBy.Value,
                AssignedTo: null, Routine: null));
        }
    }

    private static void AddTaskOccurrences(CalendarItem item, ItemSchedule.Task task, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = task.DueDate;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var dueAt = TimeZoneResolution.ResolveInstant(zoneId, date.ToDateTime(due.Time));
            var isCompleted = item.CompletionLog.Contains(new CompletionKey(date, new CompletionTarget.WholeTask()));

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                new OccurrenceTiming.Due(dueAt), due.IsAllDay, isCompleted, item.CreatedBy.Value, item.LastModifiedBy.Value, task.AssignedTo?.Value,
                Routine: null));
        }
    }

    // One occurrence per (date, subtask): each subtask's wall-clock window is computed first
    // (due.Time + a cumulative TimeSpan offset, still local time), and only then resolved through
    // TimeZoneResolution -- never by resolving the anchor to a single UTC instant and adding
    // TimeSpans to that instant, which would compute the wrong wall-clock boundary for any subtask
    // starting after a DST transition mid-routine.
    private static void AddTemplateTaskOccurrences(
        CalendarItem item, ItemSchedule.Task task, TaskTemplate template, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, List<CalendarItemOccurrence> occurrences)
    {
        var due = task.DueDate;

        foreach (var date in RecurrenceExpansion.ExpandDates(due.Date, item.Recurrence, from, to))
        {
            var offset = TimeSpan.Zero;

            foreach (var subtask in template.Subtasks)
            {
                var startLocal = date.ToDateTime(due.Time) + offset;
                var endLocal = startLocal + subtask.Duration;

                var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
                var endsAt = TimeZoneResolution.ResolveInstant(zoneId, endLocal);
                var isCompleted = item.CompletionLog.Contains(new CompletionKey(date, new CompletionTarget.Subtask(subtask.Id.Value)));

                occurrences.Add(new CalendarItemOccurrence(
                    item.Id, item.Kind, subtask.Title, subtask.Icon?.Value ?? item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                    new OccurrenceTiming.Timed(startsAt, endsAt), due.IsAllDay, isCompleted, item.CreatedBy.Value, item.LastModifiedBy.Value,
                    task.AssignedTo?.Value, new Routine(subtask.Id.Value, item.Title, item.Icon?.Value ?? calendarIcon.Value)));

                offset += subtask.Duration;
            }
        }
    }
}
