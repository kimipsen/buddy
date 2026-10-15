using buddy.Features.TaskLibrary;

namespace buddy.Features.Calendars;

// Shared by ListOccurrences and the ical feed -- expands every non-deleted item in a calendar
// into concrete occurrences within [from, to], resolved to actual instants via the calendar's
// time zone. Nothing here is persisted or cached; it's recomputed from current state every call.
// A skipped occurrence (CalendarItem.SkippedDates) is left out unless includeSkipped is set, in
// which case it comes back with IsSkipped -- only the guardian agenda asks for that
// (docs/backend/analysis/skip-occurrences.md, Questions 2 and 5).
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
        bool includeSkipped,
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
                    AddEventOccurrences(item, @event.Period, zoneId, calendarIcon, from, to, includeSkipped, occurrences);
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
                        AddTemplateTaskOccurrences(item, task, template, zoneId, calendarIcon, from, to, includeSkipped, occurrences);
                    }

                    break;

                case ItemSchedule.Task task:
                    AddTaskOccurrences(item, task, zoneId, calendarIcon, from, to, includeSkipped, occurrences);
                    break;
            }
        }

        occurrences.Sort((a, b) => a.SortAt.CompareTo(b.SortAt));

        return occurrences;
    }

    private static void AddEventOccurrences(CalendarItem item, Period period, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, bool includeSkipped, List<CalendarItemOccurrence> occurrences)
    {
        var duration = period.EndsAt.Date.ToDateTime(period.EndsAt.Time) - period.StartsAt.Date.ToDateTime(period.StartsAt.Time);

        foreach (var (date, isSkipped) in OccurringDates(item, period.StartsAt.Date, from, to, includeSkipped))
        {
            var startLocal = date.ToDateTime(period.StartsAt.Time);
            var startsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal);
            var endsAt = TimeZoneResolution.ResolveInstant(zoneId, startLocal + duration);

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                new OccurrenceTiming.Timed(startsAt, endsAt), period.IsAllDay, IsCompleted: false, isSkipped, item.Recurrence is Recurrence.Repeating, item.CreatedBy.Value, item.LastModifiedBy.Value,
                AssignedTo: null, Routine: null, item.Alarms.OffsetMinutes));
        }
    }

    private static void AddTaskOccurrences(CalendarItem item, ItemSchedule.Task task, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, bool includeSkipped, List<CalendarItemOccurrence> occurrences)
    {
        var due = task.DueDate;

        foreach (var (date, isSkipped) in OccurringDates(item, due.Date, from, to, includeSkipped))
        {
            var dueAt = TimeZoneResolution.ResolveInstant(zoneId, date.ToDateTime(due.Time));
            var isCompleted = item.CompletionLog.Contains(new CompletionKey(date, new CompletionTarget.WholeTask()));

            occurrences.Add(new CalendarItemOccurrence(
                item.Id, item.Kind, item.Title, item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                new OccurrenceTiming.Due(dueAt), due.IsAllDay, isCompleted, isSkipped, item.Recurrence is Recurrence.Repeating, item.CreatedBy.Value, item.LastModifiedBy.Value, task.AssignedTo?.Value,
                Routine: null, item.Alarms.OffsetMinutes));
        }
    }

    // One occurrence per (date, applicable subtask). A routine's subtasks run back to back: each
    // subtask's wall-clock window is computed first
    // (due.Time + a cumulative TimeSpan offset, still local time), and only then resolved through
    // TimeZoneResolution -- never by resolving the anchor to a single UTC instant and adding
    // TimeSpans to that instant, which would compute the wrong wall-clock boundary for any subtask
    // starting after a DST transition mid-routine. A checklist's items have no duration and are all
    // due at the scheduled ("leave by") time. Either kind skips an item whose weekdays exclude the
    // date (docs/backend/analysis/packing-checklists.md, Questions 2 and 3).
    private static void AddTemplateTaskOccurrences(
        CalendarItem item, ItemSchedule.Task task, TaskTemplate template, TimeZoneId zoneId, Icon calendarIcon, DateOnly from, DateOnly to, bool includeSkipped, List<CalendarItemOccurrence> occurrences)
    {
        var due = task.DueDate;
        var checklist = template.Kind == TaskTemplateKind.Checklist ? new RoutineChecklist(template.Checklist.EveningBeforeFrom) : null;

        foreach (var (date, isSkipped) in OccurringDates(item, due.Date, from, to, includeSkipped))
        {
            var offset = TimeSpan.Zero;
            var isFirstOfRun = true;

            foreach (var subtask in template.Subtasks.Where(s => s.AppliesOn(date)))
            {
                var startLocal = date.ToDateTime(due.Time) + offset;

                OccurrenceTiming timing = checklist is null
                    ? new OccurrenceTiming.Timed(
                        TimeZoneResolution.ResolveInstant(zoneId, startLocal),
                        TimeZoneResolution.ResolveInstant(zoneId, startLocal + subtask.Duration))
                    : new OccurrenceTiming.Due(TimeZoneResolution.ResolveInstant(zoneId, startLocal));
                var isCompleted = item.CompletionLog.Contains(new CompletionKey(date, new CompletionTarget.Subtask(subtask.Id.Value)));

                occurrences.Add(new CalendarItemOccurrence(
                    item.Id, item.Kind, subtask.Title, subtask.Icon?.Value ?? item.Icon?.Value ?? calendarIcon.Value, item.Icon?.Value, item.Color.Value,
                    timing, due.IsAllDay, isCompleted, isSkipped, item.Recurrence is Recurrence.Repeating, item.CreatedBy.Value, item.LastModifiedBy.Value,
                    task.AssignedTo?.Value, new Routine(subtask.Id.Value, item.Title, item.Icon?.Value ?? calendarIcon.Value, checklist),
                    isFirstOfRun ? item.Alarms.OffsetMinutes : []));

                isFirstOfRun = false;

                // Zero for a checklist item.
                offset += subtask.Duration;
            }
        }
    }

    // The rule's dates in [from, to], each flagged when the guardian skipped it; skipped dates are
    // dropped unless the caller asked for them. Applied here, not in RecurrenceExpansion.ExpandDates,
    // because RecurrenceRules' "an until leaves at least one occurrence" check is about the rule
    // alone and must ignore skips.
    private static IEnumerable<(DateOnly Date, bool IsSkipped)> OccurringDates(
        CalendarItem item, DateOnly seed, DateOnly from, DateOnly to, bool includeSkipped) =>
        RecurrenceExpansion.ExpandDates(seed, item.Recurrence, from, to)
            .Select(date => (Date: date, IsSkipped: item.SkippedDates.Contains(date)))
            .Where(d => includeSkipped || !d.IsSkipped);
}
