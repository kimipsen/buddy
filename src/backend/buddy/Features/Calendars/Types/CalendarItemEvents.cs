using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarItemEvent(
    EventItemCreated,
    TaskItemCreated,
    TemplateTaskItemCreated,
    ItemDetailsUpdated,
    EventRescheduled,
    TaskRescheduled,
    RecurrenceUpdated,
    TaskCompletionChanged,
    OccurrenceSkipChanged,
    ItemDeleted,
    ItemAlarmsUpdated
)
{
    public static CalendarItemEvent FromPayload(object payload) => payload switch
    {
        EventItemCreated e => e,
        TaskItemCreated e => e,
        TemplateTaskItemCreated e => e,
        ItemDetailsUpdated e => e,
        EventRescheduled e => e,
        TaskRescheduled e => e,
        RecurrenceUpdated e => e,
        TaskCompletionChanged e => e,
        OccurrenceSkipChanged e => e,
        ItemDeleted e => e,
        ItemAlarmsUpdated e => e,
        _ => throw new ArgumentException($"Unknown calendar item event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        EventItemCreated => nameof(EventItemCreated),
        TaskItemCreated => nameof(TaskItemCreated),
        TemplateTaskItemCreated => nameof(TemplateTaskItemCreated),
        ItemDetailsUpdated => nameof(ItemDetailsUpdated),
        EventRescheduled => nameof(EventRescheduled),
        TaskRescheduled => nameof(TaskRescheduled),
        RecurrenceUpdated => nameof(RecurrenceUpdated),
        TaskCompletionChanged => nameof(TaskCompletionChanged),
        OccurrenceSkipChanged => nameof(OccurrenceSkipChanged),
        ItemDeleted => nameof(ItemDeleted),
        ItemAlarmsUpdated => nameof(ItemAlarmsUpdated),
    };
}

public sealed record EventItemCreated(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon? Icon,
    Color Color,
    Period Period,
    Recurrence Recurrence,
    DateTimeOffset OccurredAt,
    // Null in history written before alarms existed: no alarms (calendar-item-alarms.md).
    ItemAlarms? Alarms = null);

public sealed record TaskItemCreated(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon? Icon,
    Color Color,
    DueDate DueDate,
    Recurrence Recurrence,
    DateTimeOffset OccurredAt,
    UserId? AssignedTo,
    ItemAlarms? Alarms = null);

// A task scheduled from a TaskLibrary template (ScheduleTaskFromTemplate) -- its own creation event,
// the same split EventItemCreated/TaskItemCreated already make, so neither carries a field that only
// the other uses. TaskTemplateId is a raw Guid, not TaskLibrary's TaskTemplateId type.
public sealed record TemplateTaskItemCreated(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon? Icon,
    Color Color,
    DueDate DueDate,
    Recurrence Recurrence,
    DateTimeOffset OccurredAt,
    UserId? AssignedTo,
    Guid TaskTemplateId,
    ItemAlarms? Alarms = null);

public sealed record ItemDetailsUpdated(CalendarItemId Id, ItemDetails Before, ItemDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record EventRescheduled(CalendarItemId Id, Period Before, Period After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record TaskRescheduled(CalendarItemId Id, DueDate Before, DueDate After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record RecurrenceUpdated(CalendarItemId Id, Recurrence Before, Recurrence After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// OccurrenceDate keys a single occurrence of a (possibly recurring) task, mirroring
// MedicineSchedule.DoseLog's per-occurrence keying -- completing today's instance of a daily task
// must not mark every future occurrence complete too. Target says whether it was the whole plain
// task or one subtask of a template-scheduled task's occurrence.
public sealed record TaskCompletionChanged(CalendarItemId Id, DateOnly OccurrenceDate, bool Before, bool After, UserId ModifiedBy, DateTimeOffset OccurredAt, CompletionTarget Target);

// Skips (After = true) or restores one occurrence of a repeating item without touching the rule --
// same per-date keying and Before/After shape as TaskCompletionChanged. A template-scheduled task
// skips the whole day's run, so there is no Target (docs/backend/analysis/skip-occurrences.md).
public sealed record OccurrenceSkipChanged(CalendarItemId Id, DateOnly OccurrenceDate, bool Before, bool After, UserId ModifiedBy, DateTimeOffset OccurredAt);

// The item's alarms for subscribed calendar apps (VALARM), replaced as a whole -- same Before/After
// shape as RecurrenceUpdated (docs/backend/analysis/calendar-item-alarms.md).
public sealed record ItemAlarmsUpdated(CalendarItemId Id, ItemAlarms Before, ItemAlarms After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record ItemDeleted(CalendarItemId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);
