using Marten.Events.Aggregation;

namespace buddy.Features.FlashCards;

// Marten can't use a sealed record id class as a document Id, so the snapshot wraps the plain Guid
// (docs/backend/analysis/event-stream-snapshots.md, Question 5).
public sealed record FlashCardSetSnapshot(Guid Id, FlashCardSet FlashCardSet);

// Inline snapshot of FlashCardSet in the shared "snapshots" schema: derived, rebuildable state.
public sealed class FlashCardSetSnapshotProjection : SingleStreamProjection<FlashCardSetSnapshot, Guid>
{
    public static FlashCardSetSnapshot Create(FlashCardSetCreated created) =>
        new(created.Id.Value, FlashCardSet.Start(FlashCardSetEvent.FromPayload(created)));

    public FlashCardSetSnapshot Apply(FlashCardSetSnapshot current, FlashCardSetUpdated updated) =>
        current with { FlashCardSet = FlashCardSet.Advance(current.FlashCardSet, FlashCardSetEvent.FromPayload(updated)) };

    public FlashCardSetSnapshot Apply(FlashCardSetSnapshot current, FlashCardAdded added) =>
        current with { FlashCardSet = FlashCardSet.Advance(current.FlashCardSet, FlashCardSetEvent.FromPayload(added)) };

    public FlashCardSetSnapshot Apply(FlashCardSetSnapshot current, FlashCardUpdated updated) =>
        current with { FlashCardSet = FlashCardSet.Advance(current.FlashCardSet, FlashCardSetEvent.FromPayload(updated)) };

    public FlashCardSetSnapshot Apply(FlashCardSetSnapshot current, FlashCardRemoved removed) =>
        current with { FlashCardSet = FlashCardSet.Advance(current.FlashCardSet, FlashCardSetEvent.FromPayload(removed)) };

    public FlashCardSetSnapshot Apply(FlashCardSetSnapshot current, FlashCardSetDeleted deleted) =>
        current with { FlashCardSet = FlashCardSet.Advance(current.FlashCardSet, FlashCardSetEvent.FromPayload(deleted)) };
}
