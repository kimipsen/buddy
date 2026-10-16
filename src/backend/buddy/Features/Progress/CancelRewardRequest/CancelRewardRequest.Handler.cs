namespace buddy.Features.Progress;

public static class CancelRewardRequestHandler
{
    public static Task<RewardRequestOutcome> Handle(CancelRewardRequest command, IProgressEventStore progress, TimeProvider time, CancellationToken cancellationToken) =>
        RewardRequestResolution.ResolveAsync(
            command.ChildId,
            command.RequestId,
            RewardRequestStatus.Cancelled,
            (id, now) => new RewardRequestCancelled(id, command.RequestId, now),
            progress,
            time, cancellationToken);
}
