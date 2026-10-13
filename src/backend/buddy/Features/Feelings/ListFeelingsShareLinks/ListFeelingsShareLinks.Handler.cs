using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Feelings;

// Live links only (not revoked, not expired), as ListSleepDiaryShareLinks: revoking needs the id,
// and the plaintext token was shown once.
public static class ListFeelingsShareLinksHandler
{
    public static async Task<Result<IReadOnlyCollection<FeelingsShareLinkSummary>>> Handle(
        ListFeelingsShareLinks query,
        IFeelingsShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await FeelingsAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<FeelingsShareLinkSummary>>();
        }

        var now = time.GetUtcNow();
        var documents = await shareTokens.ListForChildAsync(query.ChildId, cancellationToken);

        return new Result<IReadOnlyCollection<FeelingsShareLinkSummary>>.Success(
            [.. documents.Where(d => FeelingsShareLinks.IsLive(d, now)).Select(FeelingsShareLinkSummary.From)]);
    }
}
