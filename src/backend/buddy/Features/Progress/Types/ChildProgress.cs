using System.Collections.Immutable;

using buddy.Common.Aggregates;
using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record ChildProgress(
    ProgressId Id,
    UserId ChildId,
    int TotalStars,
    ImmutableHashSet<(CalendarItemId ItemId, DateOnly OccurrenceDate, Guid? SubtaskId)> AwardedOccurrences,
    ImmutableHashSet<int> UnlockedMilestones,
    ImmutableArray<GoalPost> GoalPosts)
{
    // A child with no progress stream yet -- what ProgressStarted folds to, so readers and the
    // first writer can work from one non-null state instead of `current?.X ?? default` chains.
    public static ChildProgress Initial(ProgressId id, UserId childId) => new(
        id,
        childId,
        0,
        ImmutableHashSet<(CalendarItemId, DateOnly, Guid?)>.Empty,
        ImmutableHashSet<int>.Empty,
        ImmutableArray<GoalPost>.Empty);

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
            AwardedOccurrences = progress.AwardedOccurrences.Add((awarded.SourceItemId, awarded.OccurrenceDate, awarded.SubtaskId))
        },
        StarRevoked revoked => progress with
        {
            TotalStars = progress.TotalStars - 1,
            AwardedOccurrences = progress.AwardedOccurrences.Remove((revoked.SourceItemId, revoked.OccurrenceDate, revoked.SubtaskId))
        },
        MilestoneUnlocked milestone => progress with
        {
            UnlockedMilestones = progress.UnlockedMilestones.Add(milestone.Threshold)
        },
        GoalPostsConfigured configured => progress with
        {
            GoalPosts = configured.GoalPosts
        },
        ProgressStarted => throw EventReplay.AlreadyStarted(nameof(ChildProgress), @event.EventType)
    };
}
