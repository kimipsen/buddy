using System.Collections.Immutable;

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
    public static ChildProgress? Rehydrate(IEnumerable<ProgressEvent> events) => events.Aggregate((ChildProgress?)null, Fold);

    // Single-event step, split out from Rehydrate so ChildProgressSnapshotProjection can drive the
    // same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and ChildProgress is
    // that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static ChildProgress? Fold(ChildProgress? progress, ProgressEvent @event) => @event switch
    {
        ProgressStarted started => new ChildProgress(
            started.Id,
            started.ChildId,
            0,
            ImmutableHashSet<(CalendarItemId, DateOnly, Guid?)>.Empty,
            ImmutableHashSet<int>.Empty,
            ImmutableArray<GoalPost>.Empty),
        StarAwarded awarded => progress! with
        {
            TotalStars = progress!.TotalStars + 1,
            AwardedOccurrences = progress!.AwardedOccurrences.Add((awarded.SourceItemId, awarded.OccurrenceDate, awarded.SubtaskId))
        },
        StarRevoked revoked => progress! with
        {
            TotalStars = progress!.TotalStars - 1,
            AwardedOccurrences = progress!.AwardedOccurrences.Remove((revoked.SourceItemId, revoked.OccurrenceDate, revoked.SubtaskId))
        },
        MilestoneUnlocked milestone => progress! with
        {
            UnlockedMilestones = progress!.UnlockedMilestones.Add(milestone.Threshold)
        },
        GoalPostsConfigured configured => progress! with
        {
            GoalPosts = configured.GoalPosts
        },
        _ => progress
    };
}
