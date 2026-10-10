using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record ChildProgress(
    ProgressId Id,
    UserId ChildId,
    int TotalStars,
    ImmutableHashSet<OccurrenceKey> AwardedOccurrences,
    ImmutableHashSet<int> UnlockedMilestones,
    ImmutableArray<GoalPost> GoalPosts,
    // The guardian's reward catalog, in display order (docs/backend/analysis/reward-redemption.md).
    ImmutableArray<Reward> Rewards,
    // Sum of the approved requests' costs. TotalStars stays lifetime and keeps driving goal posts.
    int SpentStars,
    // Every request the child made, oldest first.
    ImmutableArray<RewardRequest> RewardRequests)
{
    // Snapshots written before reward redemption have no Rewards/RewardRequests; System.Text.Json
    // then passes default(ImmutableArray), which throws on first use. Read those as empty.
    public ImmutableArray<Reward> Rewards { get; init; } = Rewards.IsDefault ? [] : Rewards;

    public ImmutableArray<RewardRequest> RewardRequests { get; init; } = RewardRequests.IsDefault ? [] : RewardRequests;

    // A child with no progress stream yet -- what ProgressStarted folds to, so readers and the
    // first writer can work from one non-null state instead of `current?.X ?? default` chains.
    public static ChildProgress Initial(ProgressId id, UserId childId) => new(
        id,
        childId,
        0,
        ImmutableHashSet<OccurrenceKey>.Empty,
        ImmutableHashSet<int>.Empty,
        ImmutableArray<GoalPost>.Empty,
        ImmutableArray<Reward>.Empty,
        0,
        ImmutableArray<RewardRequest>.Empty);

    // Stars held by pending requests, so several requests can't together overspend the balance.
    public int ReservedStars => RewardRequests.Where(r => r.Status == RewardRequestStatus.Pending).Sum(r => r.Cost);

    // What the child can still ask for. Never negative for display: a star revoked after a spend
    // can push the raw figure below zero.
    public int SpendableStars => Math.Max(0, TotalStars - SpentStars - ReservedStars);

    public static ChildProgress? Rehydrate(IEnumerable<ProgressEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static ChildProgress Replay(IEnumerable<ProgressEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // ChildProgressSnapshotProjection can drive the same logic one Marten-delivered event at a time
    // instead of duplicating this switch. Deliberately not named Apply/Create -- those names are a
    // convention JasperFx's projection source generator scans for on any type used as a projection
    // document, and ChildProgress is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static ChildProgress Start(ProgressEvent @event) => @event switch
    {
        ProgressStarted started => Initial(started.Id, started.ChildId),
        _ => throw EventReplay.NotAStartEvent(nameof(ChildProgress), @event.EventType)
    };

    public static ChildProgress Advance(ChildProgress progress, ProgressEvent @event) => @event switch
    {
        StarAwarded awarded => progress with
        {
            TotalStars = progress.TotalStars + 1,
            AwardedOccurrences = progress.AwardedOccurrences.Add(new OccurrenceKey(awarded.SourceItemId, awarded.OccurrenceDate, awarded.Target))
        },
        StarRevoked revoked => progress with
        {
            TotalStars = progress.TotalStars - 1,
            AwardedOccurrences = progress.AwardedOccurrences.Remove(new OccurrenceKey(revoked.SourceItemId, revoked.OccurrenceDate, revoked.Target))
        },
        MilestoneUnlocked milestone => progress with
        {
            UnlockedMilestones = progress.UnlockedMilestones.Add(milestone.Threshold)
        },
        GoalPostsConfigured configured => progress with
        {
            GoalPosts = configured.GoalPosts
        },
        RewardsConfigured configured => progress with
        {
            Rewards = configured.Rewards
        },
        RewardRequested requested => progress with
        {
            RewardRequests = progress.RewardRequests.Add(new RewardRequest(
                requested.RequestId,
                requested.RewardId,
                requested.Name,
                requested.Icon,
                requested.Cost,
                RewardRequestStatus.Pending,
                requested.OccurredAt,
                null,
                null))
        },
        RewardRequestApproved approved => Resolve(progress, approved.RequestId, RewardRequestStatus.Approved, approved.ApprovedBy, approved.OccurredAt),
        RewardRequestDeclined declined => Resolve(progress, declined.RequestId, RewardRequestStatus.Declined, declined.DeclinedBy, declined.OccurredAt),
        RewardRequestCancelled cancelled => Resolve(progress, cancelled.RequestId, RewardRequestStatus.Cancelled, progress.ChildId, cancelled.OccurredAt),
        ProgressStarted => throw EventReplay.AlreadyStarted(nameof(ChildProgress), @event.EventType)
    };

    public RewardRequest? FindRequest(RewardRequestId requestId) => RewardRequests.FirstOrDefault(r => r.Id == requestId);

    private static ChildProgress Resolve(ChildProgress progress, RewardRequestId requestId, RewardRequestStatus status, UserId resolvedBy, DateTimeOffset resolvedAt)
    {
        var index = progress.RewardRequests.Select(r => r.Id).ToList().IndexOf(requestId);

        if (index < 0)
        {
            throw new InvalidOperationException($"Reward request {requestId.Value} is not on progress stream {progress.Id.Value}.");
        }

        var request = progress.RewardRequests[index];

        return progress with
        {
            SpentStars = status == RewardRequestStatus.Approved ? progress.SpentStars + request.Cost : progress.SpentStars,
            RewardRequests = progress.RewardRequests.SetItem(index, request with { Status = status, ResolvedAt = resolvedAt, ResolvedBy = resolvedBy })
        };
    }
}
