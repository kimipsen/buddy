using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.FlashCards;

public static class ListMyFlashCardSetsHandler
{
    // The child's enabled sets that still exist. Config comes from the practice snapshot, never the
    // stream; a child with no practice stream has an empty list.
    public static async Task<Result<IReadOnlyCollection<PlayableFlashCardSet>>> Handle(
        ListMyFlashCardSets query,
        IFlashCardPracticeEventStore practices,
        IFlashCardSetEventStore sets,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await FlashCardAuthorization.CheckPlay(query.ChildId, guardians, cancellationToken);

        if (access != FlashCardAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<PlayableFlashCardSet>>();
        }

        var practice = await practices.FindSnapshotAsync(FlashCardPracticeId.ForChild(query.ChildId), cancellationToken);

        List<PlayableFlashCardSet> playable = [];

        foreach (var enabled in practice?.EnabledSets ?? [])
        {
            if (await sets.FindSnapshotAsync(enabled.SetId, cancellationToken) is { IsDeleted: false } set)
            {
                playable.Add(new PlayableFlashCardSet(set, enabled));
            }
        }

        return new Result<IReadOnlyCollection<PlayableFlashCardSet>>.Success(playable);
    }
}
