using Marten.Events.Aggregation;

namespace buddy.Features.Calendars;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct CalendarItemId(Guid Value)" -- as a document's Id.
// CalendarItemId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be
// CalendarItem itself; this thin wrapper carries the plain Guid Marten needs alongside the actual
// CalendarItem value. CalendarItemId stays untouched everywhere else in the codebase -- this
// wrapper exists purely at the snapshot-storage boundary. See
// docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record CalendarItemSnapshot(Guid Id, CalendarItem CalendarItem);

// Inline snapshot of CalendarItem, maintained by Marten in the same transaction as every event
// append (see CalendarsFeature.AddCalendarsFeature: options.Projections.Register(new
// CalendarItemSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "calendars" event schema -- it is derived, rebuildable state, not a second source of truth.
// CalendarItem.CompletionLog is keyed by a ValueTuple, which plain System.Text.Json can't handle
// as a dictionary key -- see ValueTupleJsonConverterFactory, registered on the same StoreOptions
// this projection's document is stored under.
public sealed class CalendarItemSnapshotProjection : SingleStreamProjection<CalendarItemSnapshot, Guid>
{
    public static CalendarItemSnapshot Create(EventItemCreated created) =>
        new(created.Id.Value, CalendarItem.Fold(null, CalendarItemEvent.FromPayload(created))!);

    public static CalendarItemSnapshot Create(TaskItemCreated created) =>
        new(created.Id.Value, CalendarItem.Fold(null, CalendarItemEvent.FromPayload(created))!);

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, ItemDetailsUpdated updated) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(updated))! };

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, EventRescheduled rescheduled) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(rescheduled))! };

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, TaskRescheduled rescheduled) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(rescheduled))! };

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, RecurrenceUpdated updated) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(updated))! };

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, TaskCompletionChanged completion) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(completion))! };

    public CalendarItemSnapshot Apply(CalendarItemSnapshot current, ItemDeleted deleted) =>
        current with { CalendarItem = CalendarItem.Fold(current.CalendarItem, CalendarItemEvent.FromPayload(deleted))! };
}
