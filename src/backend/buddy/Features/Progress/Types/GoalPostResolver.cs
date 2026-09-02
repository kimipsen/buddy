using System.Collections.Immutable;

namespace buddy.Features.Progress;

// Shared by RecordStarChangeHandler (to detect a newly-crossed goal post) and
// GetChildProgress/GetMyProgress/ConfigureGoalPosts (to resolve what to display), so the
// extrapolation logic that keeps a child's progress moving past their guardian's configured
// list exists in exactly one place. See docs/backend/analysis/configurable-goal-posts.md.
public static class GoalPostResolver
{
    // Falls back to the scale gamified-progress.md originally shipped, for a child whose guardian
    // hasn't configured anything yet -- zero migration needed for existing ChildProgress streams.
    public static readonly ImmutableArray<GoalPost> DefaultGoalPosts =
    [
        new GoalPost(5, "🌱", null),
        new GoalPost(10, "🌿", null),
        new GoalPost(25, "🪴", null),
        new GoalPost(50, "🌳", null),
        new GoalPost(100, "🏆", null)
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
    // Step and icons cycle back through the configured list, with Round counting how many full
    // passes through that list have happened -- the frontend uses Round to show e.g. "🌳 ×2".
    public static ResolvedGoalPost At(ImmutableArray<GoalPost> configured, int index)
    {
        var posts = Effective(configured);
        var round = index / posts.Length + 1;
        var post = posts[index % posts.Length];

        if (round == 1)
        {
            return new ResolvedGoalPost(post.Threshold, post.Icon, post.Label, round);
        }

        var threshold = posts[^1].Threshold + Step(posts) * (index - posts.Length + 1);

        return new ResolvedGoalPost(threshold, post.Icon, post.Label, round);
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

public sealed record ResolvedGoalPost(int Threshold, string Icon, string? Label, int Round);
