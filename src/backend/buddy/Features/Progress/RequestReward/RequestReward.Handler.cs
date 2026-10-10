namespace buddy.Features.Progress;

public static class RequestRewardHandler
{
    public static async Task<RewardRequestOutcome> Handle(RequestReward command, IProgressEventStore progress, CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(command.ChildId);
        var current = ChildProgress.Rehydrate(await progress.ReadAsync(id, cancellationToken));

        // No stream means no catalog either, so the reward can't exist.
        if (current?.Rewards.FirstOrDefault(r => r.Id == command.RewardId) is not { } reward)
        {
            return new RewardRequestOutcome.NotFound();
        }

        if (current.RewardRequests.Count(r => r.Status == RewardRequestStatus.Pending) >= RewardRequestRules.MaxPendingRequests)
        {
            return new RewardRequestOutcome.TooManyPendingRequests();
        }

        if (reward.Cost > current.SpendableStars)
        {
            return new RewardRequestOutcome.InsufficientStars();
        }

        var requested = new RewardRequested(id, RewardRequestId.New(), reward.Id, reward.Name, reward.Icon, reward.Cost, DateTimeOffset.UtcNow);
        await progress.AppendAsync(id, [requested], cancellationToken);

        return new RewardRequestOutcome.Success(ProgressSummary.From(ChildProgress.Advance(current, requested)));
    }
}
