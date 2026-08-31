using System.Collections.Immutable;

namespace buddy.Features.Progress;

public sealed record ProgressSummary(
    int TotalStars,
    IReadOnlyList<int> UnlockedMilestones,
    string? CurrentIcon,
    int NextGoalThreshold,
    string NextGoalIcon,
    IReadOnlyList<GoalPostResponse> GoalPosts)
{
    public static ProgressSummary From(ChildProgress? progress)
    {
        var totalStars = progress?.TotalStars ?? 0;
        var unlockedMilestones = progress?.UnlockedMilestones ?? ImmutableHashSet<int>.Empty;
        var configuredGoalPosts = progress?.GoalPosts ?? ImmutableArray<GoalPost>.Empty;
        var (current, next) = GoalPostResolver.Resolve(configuredGoalPosts, totalStars);

        return new ProgressSummary(
            totalStars,
            [.. unlockedMilestones.OrderBy(threshold => threshold)],
            current?.Icon,
            next.Threshold,
            next.Icon,
            [.. GoalPostResolver.Effective(configuredGoalPosts).Select(post => new GoalPostResponse(post.Threshold, post.Icon, post.Label))]);
    }
}

public sealed record GoalPostResponse(int Threshold, string Icon, string? Label);
