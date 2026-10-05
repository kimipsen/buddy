using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.SleepDiaries;

// Not in the design doc's slice table: revoking a link needs its id, and the plaintext token is
// shown only once, so the guardian needs a list of the links still live.
public static class ListSleepDiaryShareLinksHandler
{
    public static async Task<Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>> Handle(
        ListSleepDiaryShareLinks query,
        ISleepDiaryShareTokenEventStore shareTokens,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await SleepDiaryAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != SleepDiaryAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<SleepDiaryShareLinkSummary>>();
        }

        var now = DateTimeOffset.UtcNow;
        var documents = await shareTokens.ListForChildAsync(query.ChildId, cancellationToken);

        return new Result<IReadOnlyCollection<SleepDiaryShareLinkSummary>>.Success(
            [.. documents.Where(d => SleepDiaryShareLinks.IsLive(d, now)).Select(SleepDiaryShareLinkSummary.From)]);
    }
}
