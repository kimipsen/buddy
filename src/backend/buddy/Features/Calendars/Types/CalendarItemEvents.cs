using buddy.Features.Users;

namespace buddy.Features.Calendars;

public union CalendarItemEvent(
    EventItemCreated,
    TaskItemCreated,
    ItemDetailsUpdated,
    EventRescheduled,
    TaskRescheduled,
    RecurrenceUpdated,
    ItemDeleted
)
{
    public static CalendarItemEvent FromPayload(object payload) => payload switch
    {
        EventItemCreated e => e,
        TaskItemCreated e => e,
        ItemDetailsUpdated e => e,
        EventRescheduled e => e,
        TaskRescheduled e => e,
        RecurrenceUpdated e => e,
        ItemDeleted e => e,
        _ => throw new ArgumentException($"Unknown calendar item event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        EventItemCreated => nameof(EventItemCreated),
        TaskItemCreated => nameof(TaskItemCreated),
        ItemDetailsUpdated => nameof(ItemDetailsUpdated),
        EventRescheduled => nameof(EventRescheduled),
        TaskRescheduled => nameof(TaskRescheduled),
        RecurrenceUpdated => nameof(RecurrenceUpdated),
        ItemDeleted => nameof(ItemDeleted),
    };
}

public sealed record EventItemCreated(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon Icon,
    Color Color,
    Period Period,
    RecurrenceRule? Recurrence,
    DateTimeOffset OccurredAt);

public sealed record TaskItemCreated(
    CalendarItemId Id,
    CalendarId CalendarId,
    UserId CreatedBy,
    string Title,
    Icon Icon,
    Color Color,
    DueDate DueDate,
    RecurrenceRule? Recurrence,
    DateTimeOffset OccurredAt);

public sealed record ItemDetailsUpdated(CalendarItemId Id, ItemDetails Before, ItemDetails After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record EventRescheduled(CalendarItemId Id, Period Before, Period After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record TaskRescheduled(CalendarItemId Id, DueDate Before, DueDate After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record RecurrenceUpdated(CalendarItemId Id, RecurrenceRule? Before, RecurrenceRule? After, UserId ModifiedBy, DateTimeOffset OccurredAt);

public sealed record ItemDeleted(CalendarItemId Id, UserId ModifiedBy, DateTimeOffset OccurredAt);
