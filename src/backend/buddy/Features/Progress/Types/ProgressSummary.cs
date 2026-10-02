namespace buddy.Features.Progress;

public sealed record ProgressSummary(
    int TotalStars,
    IReadOnlyList<int> UnlockedMilestones,
    string? CurrentIcon,
    int NextGoalThreshold,
    string NextGoalIcon,
    IReadOnlyList<GoalPostResponse> GoalPosts)
{
    public static ProgressSummary From(ChildProgress progress)
    {
        var configuredGoalPosts = progress.GoalPosts;
        var (current, next) = GoalPostResolver.Resolve(configuredGoalPosts, progress.TotalStars);

        return new ProgressSummary(
            progress.TotalStars,
            [.. progress.UnlockedMilestones.OrderBy(threshold => threshold)],
            current?.Icon,
            next.Threshold,
            next.Icon,
            [.. GoalPostResolver.Effective(configuredGoalPosts).Select(post => new GoalPostResponse(post.Threshold, post.Icon, post.Label))]);
    }
}

public sealed record GoalPostResponse(int Threshold, string Icon, string Label);
