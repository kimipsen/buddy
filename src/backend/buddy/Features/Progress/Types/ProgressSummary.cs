namespace buddy.Features.Progress;

public sealed record ProgressSummary(
    int TotalStars,
    IReadOnlyList<int> UnlockedMilestones,
    // The reached goal post's icon, or the next one's before the first is reached.
    string DisplayIcon,
    int NextGoalThreshold,
    string NextGoalIcon,
    IReadOnlyList<GoalPostResponse> GoalPosts,
    // TotalStars minus spent and reserved (pending) stars; what the child can still ask for.
    int SpendableStars,
    int SpentStars,
    IReadOnlyList<RewardResponse> Rewards,
    // Pending requests oldest first, then the most recently resolved ones, newest first.
    IReadOnlyList<RewardRequestResponse> RewardRequests)
{
    // Keeps the summary bounded; the personal data export reads the aggregate, not this.
    public const int ResolvedRequestHistory = 20;

    public static ProgressSummary From(ChildProgress progress)
    {
        var configuredGoalPosts = progress.GoalPosts;
        var (current, next) = GoalPostResolver.Resolve(configuredGoalPosts, progress.TotalStars);

        var pending = progress.RewardRequests.Where(r => r.Status == RewardRequestStatus.Pending);
        var resolved = progress.RewardRequests
            .Where(r => r.Status != RewardRequestStatus.Pending)
            .OrderByDescending(r => r.ResolvedAt)
            .Take(ResolvedRequestHistory);

        return new ProgressSummary(
            progress.TotalStars,
            [.. progress.UnlockedMilestones.OrderBy(threshold => threshold)],
            current?.Icon ?? next.Icon,
            next.Threshold,
            next.Icon,
            [.. GoalPostResolver.Effective(configuredGoalPosts).Select(post => new GoalPostResponse(post.Threshold, post.Icon, post.Label))],
            progress.SpendableStars,
            progress.SpentStars,
            [.. progress.Rewards.Select(RewardResponse.From)],
            [.. pending.Concat(resolved).Select(RewardRequestResponse.From)]);
    }
}

public sealed record GoalPostResponse(int Threshold, string Icon, string Label);

public sealed record RewardResponse(Guid Id, string Name, string Icon, int Cost)
{
    public static RewardResponse From(Reward reward) => new(reward.Id.Value, reward.Name, reward.Icon, reward.Cost);
}

public sealed record RewardRequestResponse(
    Guid Id,
    Guid RewardId,
    string Name,
    string Icon,
    int Cost,
    RewardRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ResolvedAt)
{
    public static RewardRequestResponse From(RewardRequest request) => new(
        request.Id.Value, request.RewardId.Value, request.Name, request.Icon, request.Cost, request.Status, request.RequestedAt, request.ResolvedAt);
}
