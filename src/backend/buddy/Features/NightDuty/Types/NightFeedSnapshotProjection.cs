using Marten.Events.Aggregation;

namespace buddy.Features.NightDuty;

// The same thin wrapper as NightScheduleSnapshot: Marten can't use the class-based NightFeedId as a
// document id. See docs/backend/analysis/event-stream-snapshots.md, Question 5.
public sealed record NightFeedSnapshot(Guid Id, NightFeed NightFeed);

// Inline snapshot of NightFeed in the shared "snapshots" schema, registered in
// NightDutyFeature.AddNightDutyFeature.
public sealed class NightFeedSnapshotProjection : SingleStreamProjection<NightFeedSnapshot, Guid>
{
    public static NightFeedSnapshot Create(NightFeedStarted started) =>
        new(started.Id.Value, NightFeed.Start(NightFeedEvent.FromPayload(started)));

    public NightFeedSnapshot Apply(NightFeedSnapshot current, NightIcalTokenIssued issued) =>
        current with { NightFeed = NightFeed.Advance(current.NightFeed, NightFeedEvent.FromPayload(issued)) };

    public NightFeedSnapshot Apply(NightFeedSnapshot current, NightIcalTokenRevoked revoked) =>
        current with { NightFeed = NightFeed.Advance(current.NightFeed, NightFeedEvent.FromPayload(revoked)) };
}
