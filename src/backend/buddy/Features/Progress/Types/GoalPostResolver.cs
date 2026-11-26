using System.Collections.Immutable;

namespace buddy.Features.Progress;

// Shared by RecordStarChangeHandler (to detect a newly-crossed goal post) and
// GetChildProgress/GetMyProgress/ConfigureGoalPosts (to resolve what to display), so the
// extrapolation logic that keeps a child's progress moving past their guardian's configured
// list exists in exactly one place. See docs/backend/analysis/configurable-goal-posts.md.
public static class GoalPostResolver
{
    // The scale gamified-progress.md describes, used for a child whose guardian hasn't configured
    // anything yet. No captions ("" means none).
    public static readonly ImmutableArray<GoalPost> DefaultGoalPosts =
    [
        new GoalPost(5, "🌱", ""),
        new GoalPost(10, "🌿", ""),
        new GoalPost(25, "🪴", ""),
        new GoalPost(50, "🌳", ""),
        new GoalPost(100, "🏆", "")
    ];

    public static ImmutableArray<GoalPost> Effective(ImmutableArray<GoalPost> configured) =>
        configured.IsDefaultOrEmpty ? DefaultGoalPosts : configured;

    // The gap used to keep generating goal posts once a child passes the last configured one --
    // the distance between the last two configured thresholds, or the single threshold itself
    // when only one is configured. Guarded to be positive so the sequence in At() always climbs.
    private static int Step(ImmutableArray<GoalPost> posts) =>
        posts.Length >= 2 ? Math.Max(1, posts[^1].Threshold - posts[^2].Threshold) : Math.Max(1, posts[0].Threshold);

    // Resolves the goal post at a position in the infinite sequence: index 0..posts.Length-1 are
    // the guardian's own configured posts (Round 1); beyond that, thresholds keep climbing by
    // Step and icons cycle back through the configured list (round 2, 3, ...).
    public static ResolvedGoalPost At(ImmutableArray<GoalPost> configured, int index)
    {
        var posts = Effective(configured);
        var round = index / posts.Length + 1;
        var post = posts[index % posts.Length];

        if (round == 1)
        {
            return new ResolvedGoalPost(post.Threshold, post.Icon);
        }

        var threshold = posts[^1].Threshold + Step(posts) * (index - posts.Length + 1);

        return new ResolvedGoalPost(threshold, post.Icon);
    }

    // Finds the goal post -- real or extrapolated -- whose threshold exactly equals `stars`, if
    // any. Used by RecordStarChangeHandler to decide whether a MilestoneUnlocked was just crossed.
    // Terminates because thresholds strictly increase with index (ascending config, positive step).
    public static ResolvedGoalPost? AtThreshold(ImmutableArray<GoalPost> configured, int stars)
    {
        var index = 0;

        while (true)
        {
            var resolved = At(configured, index);

            if (resolved.Threshold == stars)
            {
                return resolved;
            }

            if (resolved.Threshold > stars)
            {
                return null;
            }

            index++;
        }
    }

    // Every goal post -- real or extrapolated -- whose threshold lies in (fromExclusive, toInclusive],
    // ascending. A flash card award can jump several thresholds at once, unlike a task star's +1.
    // Like AtThreshold it only reports what was crossed; the caller skips thresholds already in
    // ChildProgress.UnlockedMilestones (a revoked star never removes one, so recrossing it later
    // emits nothing, exactly as for a task star).
    public static IReadOnlyList<ResolvedGoalPost> CrossedBetween(ImmutableArray<GoalPost> configured, int fromExclusive, int toInclusive)
    {
        List<ResolvedGoalPost> crossed = [];
        var index = 0;

        while (true)
        {
            var resolved = At(configured, index);

            if (resolved.Threshold > toInclusive)
            {
                return crossed;
            }

            if (resolved.Threshold > fromExclusive)
            {
                crossed.Add(resolved);
            }

            index++;
        }
    }

    // Resolves what a child should currently see: the highest goal post already reached (null if
    // none yet), and the next one ahead.
    public static (ResolvedGoalPost? Current, ResolvedGoalPost Next) Resolve(ImmutableArray<GoalPost> configured, int totalStars)
    {
        ResolvedGoalPost? current = null;
        var index = 0;

        while (true)
        {
            var candidate = At(configured, index);

            if (candidate.Threshold > totalStars)
            {
                return (current, candidate);
            }

            current = candidate;
            index++;
        }
    }
}

public sealed record ResolvedGoalPost(int Threshold, string Icon);
