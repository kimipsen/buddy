using System.Collections.Immutable;
using System.Text.Json.Serialization;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// Icon is null when the item has no override -- it inherits the owning Calendar's Icon. Resolving
// that fallback is a rendering concern, not this aggregate's: see CalendarOccurrenceExpansion,
// the one place effective icon is computed, shared by ListOccurrences and the ical feed.
public sealed record CalendarItem(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon? Icon,
    Color Color,
    ItemSchedule Schedule,
    RecurrenceRule? Recurrence,
    ImmutableDictionary<(DateOnly OccurrenceDate, Guid? SubtaskId), bool> CompletionLog,
    UserId LastModifiedBy,
    bool IsDeleted)
{
    // Derived from Schedule (the wire still sends kind: 0|1), so kept out of the snapshot JSON.
    [JsonIgnore]
    public CalendarItemKind Kind => Schedule switch
    {
        ItemSchedule.Event => CalendarItemKind.Event,
        ItemSchedule.Task => CalendarItemKind.Task,
    };

    // Sort key for calendar listings: an event sorts by its own start, a task by its due date.
    // A plain local DateTime is fine here -- it's only used to order items within one calendar,
    // which all share the same time zone, not to resolve an actual instant.
    [JsonIgnore]
    public DateTime ScheduleKey => Schedule switch
    {
        ItemSchedule.Event @event => @event.Period.StartsAt.Date.ToDateTime(@event.Period.StartsAt.Time),
        ItemSchedule.Task task => task.DueDate.Date.ToDateTime(task.DueDate.Time),
    };

    public static CalendarItem? Rehydrate(IEnumerable<CalendarItemEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static CalendarItem Replay(IEnumerable<CalendarItemEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // CalendarItemSnapshotProjection can drive the same logic one Marten-delivered event at a time
    // instead of duplicating this switch. Deliberately not named Apply/Create -- those names are a
    // convention JasperFx's projection source generator scans for on any type used as a projection
    // document, and CalendarItem is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static CalendarItem Start(CalendarItemEvent @event) => @event switch
    {
        EventItemCreated created => New(created.Id, created.CalendarId, created.CreatedBy, created.Title, created.Icon, created.Color,
            new ItemSchedule.Event(created.Period), created.Recurrence),
        TaskItemCreated created => New(created.Id, created.CalendarId, created.CreatedBy, created.Title, created.Icon, created.Color,
            new ItemSchedule.Task(created.DueDate, created.AssignedTo, new TaskSource.Freeform()), created.Recurrence),
        TemplateTaskItemCreated created => New(created.Id, created.CalendarId, created.CreatedBy, created.Title, created.Icon, created.Color,
            new ItemSchedule.Task(created.DueDate, created.AssignedTo, new TaskSource.FromTemplate(created.TaskTemplateId)), created.Recurrence),
        _ => throw EventReplay.NotAStartEvent(nameof(CalendarItem), @event.EventType)
    };

    public static CalendarItem Advance(CalendarItem item, CalendarItemEvent @event) => @event switch
    {
        ItemDetailsUpdated updated => item with { Title = updated.After.Title, Icon = updated.After.Icon, Color = updated.After.Color, LastModifiedBy = updated.ModifiedBy },
        // RescheduleItem only appends the event matching the item's own case, so a mismatch is
        // corrupt history rather than a request error.
        EventRescheduled rescheduled => item.Schedule switch
        {
            ItemSchedule.Event => item with { Schedule = new ItemSchedule.Event(rescheduled.After), LastModifiedBy = rescheduled.ModifiedBy },
            ItemSchedule.Task => throw new InvalidOperationException($"EventRescheduled on task item {item.Id.Value}."),
        },
        TaskRescheduled rescheduled => item.Schedule switch
        {
            ItemSchedule.Task task => item with { Schedule = task with { DueDate = rescheduled.After }, LastModifiedBy = rescheduled.ModifiedBy },
            ItemSchedule.Event => throw new InvalidOperationException($"TaskRescheduled on event item {item.Id.Value}."),
        },
        RecurrenceUpdated recurrence => item with { Recurrence = recurrence.After, LastModifiedBy = recurrence.ModifiedBy },
        // Sparse log, same rule as MedicineSchedule.DoseLog: "not completed" is the
        // implicit default, so a not-completed entry is removed rather than stored. Keyed
        // by (OccurrenceDate, SubtaskId) so a template-scheduled task's subtasks complete
        // independently; a plain non-template task always keys as (date, null).
        TaskCompletionChanged completion => item with
        {
            CompletionLog = completion.After
                ? item.CompletionLog.SetItem((completion.OccurrenceDate, completion.SubtaskId), true)
                : item.CompletionLog.Remove((completion.OccurrenceDate, completion.SubtaskId)),
            LastModifiedBy = completion.ModifiedBy
        },
        ItemDeleted deleted => item with { IsDeleted = true, LastModifiedBy = deleted.ModifiedBy },
        EventItemCreated or TaskItemCreated or TemplateTaskItemCreated => throw EventReplay.AlreadyStarted(nameof(CalendarItem), @event.EventType)
    };

    private static CalendarItem New(
        CalendarItemId id, CalendarId calendarId, UserId createdBy, string title, Icon? icon, Color color, ItemSchedule schedule, RecurrenceRule? recurrence) =>
        new(id, calendarId, createdBy, title, icon, color, schedule, recurrence,
            ImmutableDictionary<(DateOnly, Guid?), bool>.Empty, createdBy, IsDeleted: false);
}
