using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Progress;

public static class GetChildProgressHandler
{
    public static async Task<Result<ProgressSummary>> Handle(
        GetChildProgress query, IProgressEventStore progress, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.CallerId is not { } callerId)
        {
            return new Result<ProgressSummary>.NotFound();
        }

        var access = await ProgressAuthorization.CheckView(query.ChildId, callerId, guardians, cancellationToken);

        if (access != ProgressAccess.Allowed)
        {
            return access.ToDeniedResult<ProgressSummary>();
        }

        var id = ProgressId.ForChild(query.ChildId);
        var current = await progress.FindSnapshotAsync(id, cancellationToken);

        return new Result<ProgressSummary>.Success(ProgressSummary.From(current));
    }
}
