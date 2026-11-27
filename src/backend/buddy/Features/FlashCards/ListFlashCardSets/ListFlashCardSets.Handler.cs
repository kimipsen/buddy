using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class ListFlashCardSetsHandler
{
    // The caller's own sets plus those of their co-guardians (soft-deleted sets are left out), then
    // the built-in catalogue in its own order.
    public static async Task<Result<IReadOnlyCollection<FlashCardSet>>> Handle(
        ListFlashCardSets query,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        IBuiltInCatalogue catalogue,
        CancellationToken cancellationToken)
    {
        var access = await FlashCardAuthorization.CheckManageLibrary(query.UserId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<FlashCardSet>>();
        }

        var owners = await FlashCardAuthorization.LibraryOwners(query.UserId, guardians, cancellationToken);
        var ids = await sets.ListIdsByOwnersAsync(owners, cancellationToken);

        List<FlashCardSet> library = [];

        foreach (var id in ids)
        {
            if (await sets.FindSnapshotAsync(id, cancellationToken) is { IsDeleted: false } set)
            {
                library.Add(set);
            }
        }

        // UUIDv7 ids sort by creation time; the built-in ids are UUIDv5 and follow in catalogue order.
        IReadOnlyCollection<FlashCardSet> ordered = [.. library.OrderBy(s => s.Id.Value), .. catalogue.All];

        return new Result<IReadOnlyCollection<FlashCardSet>>.Success(ordered);
    }
}
