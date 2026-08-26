using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Progress;

public static class GetChildProgressHandler
{
    // Deliberately a single tier, unlike MedicineAuthorization's Mark/Manage split -- there is no
    // guardian write action on a child's progress yet (no redemption, no manual adjustment), so
    // "self or an active guardian" is the whole check for now.
    public static async Task<Result<ProgressSummary>> Handle(
        GetChildProgress query, IProgressEventStore progress, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.CallerId is not { } callerId)
        {
            return new Result<ProgressSummary>.NotFound();
        }

        if (callerId != query.ChildId && await guardians.FindActiveLinkAsync(query.ChildId, callerId, cancellationToken) is null)
        {
            // Collapsed to NotFound, not Forbidden -- same "can't distinguish no-such-child from
            // not-your-child" reasoning MedicineAccess already applies.
            return new Result<ProgressSummary>.NotFound();
        }

        var id = ProgressId.ForChild(query.ChildId);
        var events = await progress.ReadAsync(id, cancellationToken);
        var current = ChildProgress.Rehydrate(events);

        return new Result<ProgressSummary>.Success(
            current is null ? new ProgressSummary(0, []) : new ProgressSummary(current.TotalStars, [.. current.UnlockedMilestones.OrderBy(t => t)]));
    }
}
