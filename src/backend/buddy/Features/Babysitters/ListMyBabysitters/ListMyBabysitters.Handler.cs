using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Babysitters;

public static class ListMyBabysittersHandler
{
    public static async Task<Result<IReadOnlyCollection<BabysitterSummary>>> Handle(
        ListMyBabysitters query,
        IBabysitterListEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var access = await BabysitterAuthorization.CheckManage(userId, guardians, cancellationToken);

        if (access != BabysitterAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<BabysitterSummary>>();
        }

        // No stream yet reads as the same empty list BabysitterListStarted would fold to. Archived
        // babysitters are included (flagged) in the order they were added.
        var list = await store.FindSnapshotAsync(BabysitterListId.ForGuardian(userId), cancellationToken)
            ?? BabysitterList.Empty(userId);

        return new Result<IReadOnlyCollection<BabysitterSummary>>.Success([.. list.Babysitters.Select(BabysitterSummary.From)]);
    }
}
