using Marten.Events.Aggregation;

namespace buddy.Features.Pickups;

// The same thin wrapper as PickupScheduleSnapshot: Marten can't use the class-based PickupFeedId as
// a document id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record PickupFeedSnapshot(Guid Id, PickupFeed PickupFeed);

// Inline snapshot of PickupFeed in the shared "snapshots" schema, registered in
// PickupsFeature.AddPickupsFeature.
public sealed class PickupFeedSnapshotProjection : SingleStreamProjection<PickupFeedSnapshot, Guid>
{
    public static PickupFeedSnapshot Create(PickupFeedStarted started) =>
        new(started.Id.Value, PickupFeed.Start(PickupFeedEvent.FromPayload(started)));

    public PickupFeedSnapshot Apply(PickupFeedSnapshot current, PickupIcalTokenIssued issued) =>
        current with { PickupFeed = PickupFeed.Advance(current.PickupFeed, PickupFeedEvent.FromPayload(issued)) };

    public PickupFeedSnapshot Apply(PickupFeedSnapshot current, PickupIcalTokenRevoked revoked) =>
        current with { PickupFeed = PickupFeed.Advance(current.PickupFeed, PickupFeedEvent.FromPayload(revoked)) };
}
