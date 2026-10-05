using Marten.Events.Aggregation;

namespace buddy.Features.Babysitters;

// Wrapper document for the same reason as PickupScheduleSnapshot: Marten can't use a sealed-record
// id class as a document Id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record BabysitterListSnapshot(Guid Id, BabysitterList BabysitterList);

// Inline snapshot of BabysitterList, maintained in the same transaction as every event append and
// stored in the shared "snapshots" schema -- derived, rebuildable state.
public sealed class BabysitterListSnapshotProjection : SingleStreamProjection<BabysitterListSnapshot, Guid>
{
    public static BabysitterListSnapshot Create(BabysitterListStarted started) =>
        new(started.Id.Value, BabysitterList.Start(BabysitterEvent.FromPayload(started)));

    public BabysitterListSnapshot Apply(BabysitterListSnapshot current, BabysitterAdded e) => Next(current, e);

    public BabysitterListSnapshot Apply(BabysitterListSnapshot current, BabysitterDetailsChanged e) => Next(current, e);

    public BabysitterListSnapshot Apply(BabysitterListSnapshot current, BabysitterArchived e) => Next(current, e);

    private static BabysitterListSnapshot Next(BabysitterListSnapshot current, BabysitterEvent e) =>
        current with { BabysitterList = BabysitterList.Advance(current.BabysitterList, e) };
}
