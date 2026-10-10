using Marten.Events.Aggregation;

namespace buddy.Features.HouseRules;

// Thin wrapper: Marten can't use the sealed-record RuleBookId as a document Id. See
// PickupScheduleSnapshot and docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record RuleBookSnapshot(Guid Id, RuleBook RuleBook);

// Inline snapshot in the shared "snapshots" schema -- derived, rebuildable state, never a second
// source of truth.
public sealed class RuleBookSnapshotProjection : SingleStreamProjection<RuleBookSnapshot, Guid>
{
    public static RuleBookSnapshot Create(RuleBookStarted started) =>
        new(started.Id.Value, RuleBook.Start(RuleBookEvent.FromPayload(started)));

    public RuleBookSnapshot Apply(RuleBookSnapshot current, RuleAdded added) =>
        current with { RuleBook = RuleBook.Advance(current.RuleBook, RuleBookEvent.FromPayload(added)) };

    public RuleBookSnapshot Apply(RuleBookSnapshot current, RuleEdited edited) =>
        current with { RuleBook = RuleBook.Advance(current.RuleBook, RuleBookEvent.FromPayload(edited)) };

    public RuleBookSnapshot Apply(RuleBookSnapshot current, RuleRemoved removed) =>
        current with { RuleBook = RuleBook.Advance(current.RuleBook, RuleBookEvent.FromPayload(removed)) };

    public RuleBookSnapshot Apply(RuleBookSnapshot current, RulesReordered reordered) =>
        current with { RuleBook = RuleBook.Advance(current.RuleBook, RuleBookEvent.FromPayload(reordered)) };

    public RuleBookSnapshot Apply(RuleBookSnapshot current, RuleAcknowledged acknowledged) =>
        current with { RuleBook = RuleBook.Advance(current.RuleBook, RuleBookEvent.FromPayload(acknowledged)) };
}
