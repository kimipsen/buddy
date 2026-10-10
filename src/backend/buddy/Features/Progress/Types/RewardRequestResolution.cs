using buddy.Features.Users;

namespace buddy.Features.Progress;

// The part ApproveRewardRequest, DeclineRewardRequest and CancelRewardRequest share once the caller
// is authorized: find the request, apply the Pending -> target transition, and treat repeating the
// same transition as an idempotent success. See docs/backend/analysis/reward-redemption.md, Question 3.
public static class RewardRequestResolution
{
    public static async Task<RewardRequestOutcome> ResolveAsync(
        UserId childId,
        RewardRequestId requestId,
        RewardRequestStatus target,
        Func<ProgressId, DateTimeOffset, ProgressEvent> resolution,
        IProgressEventStore progress,
        CancellationToken cancellationToken)
    {
        var id = ProgressId.ForChild(childId);
        var current = ChildProgress.Rehydrate(await progress.ReadAsync(id, cancellationToken));

        if (current?.FindRequest(requestId) is not { } request)
        {
            return new RewardRequestOutcome.NotFound();
        }

        if (request.Status == target)
        {
            return new RewardRequestOutcome.Success(ProgressSummary.From(current));
        }

        if (request.Status != RewardRequestStatus.Pending)
        {
            return new RewardRequestOutcome.AlreadyResolved();
        }

        // A star revoked since the request may have eaten into its reservation; never spend into
        // a negative balance. The other pending requests are re-checked when they're approved.
        if (target == RewardRequestStatus.Approved && request.Cost > current.TotalStars - current.SpentStars)
        {
            return new RewardRequestOutcome.InsufficientStars();
        }

        var @event = resolution(id, DateTimeOffset.UtcNow);
        await progress.AppendAsync(id, [@event], cancellationToken);

        return new RewardRequestOutcome.Success(ProgressSummary.From(ChildProgress.Advance(current, @event)));
    }
}
