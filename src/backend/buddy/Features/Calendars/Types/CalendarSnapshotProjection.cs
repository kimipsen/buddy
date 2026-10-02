using Marten.Events.Aggregation;

namespace buddy.Features.Calendars;

// Marten's document identity resolution only supports a plain Guid/string/int/long -- or a
// wrapper struct like "readonly record struct CalendarId(Guid Value)" -- as a document's Id.
// CalendarId here is a sealed record (a class), which Marten's DocumentMapping rejects with
// "Could not determine an 'id/Id' field or property". So the snapshot document can't be Calendar
// itself; this thin wrapper carries the plain Guid Marten needs alongside the actual Calendar
// value. CalendarId stays untouched everywhere else in the codebase -- this wrapper exists purely
// at the snapshot-storage boundary. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record CalendarSnapshot(Guid Id, Calendar Calendar);

// Inline snapshot of Calendar, maintained by Marten in the same transaction as every event append
// (see CalendarsFeature.AddCalendarsFeature: options.Projections.Register(new
// CalendarSnapshotProjection(), ...)). Stored in the shared "snapshots" schema, never the
// "calendars" event schema -- it is derived, rebuildable state, not a second source of truth.
public sealed class CalendarSnapshotProjection : SingleStreamProjection<CalendarSnapshot, Guid>
{
    public static CalendarSnapshot Create(CalendarCreatedForGroup created) =>
        new(created.CalendarId.Value, Calendar.Start(CalendarEvent.FromPayload(created)));

    public CalendarSnapshot Apply(CalendarSnapshot current, CalendarIconChanged changed) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(changed)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, CalendarTransferredToGroup transferred) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(transferred)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, MemberRoleGranted granted) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(granted)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, MemberRoleRevoked revoked) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(revoked)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, IcalTokenIssued issued) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(issued)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, IcalTokenRevoked revoked) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(revoked)) };

    public CalendarSnapshot Apply(CalendarSnapshot current, CalendarDeleted deleted) =>
        current with { Calendar = Calendar.Advance(current.Calendar, CalendarEvent.FromPayload(deleted)) };
}
