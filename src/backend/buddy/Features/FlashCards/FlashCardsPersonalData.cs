using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// A child's practice (enabled sets, tiers, every finished game with its per-card results) is theirs
// alone, keyed by their UserId: erasing the child deletes its stream and snapshot, including the
// derived per-card weights held in that snapshot. See gdpr-data-protection.md.
//
// Sets are the library of the guardian who created them. IPersonalDataEraser gives a guardian no
// heir, so an erased guardian's sets are soft-deleted: they leave every list and can no longer be
// enabled or played, while a child's finished games keep the set name they were played under.
public sealed class FlashCardsPersonalDataEraser(IFlashCardsStore store, IFlashCardSetEventStore sets, TimeProvider time) : IPersonalDataEraser
{
    public Type Store => typeof(IFlashCardsStore);

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        foreach (var setId in await sets.ListIdsByOwnersAsync([guardian.UserId], cancellationToken))
        {
            // Idempotent: a set already deleted (by the guardian or an earlier run) is left alone.
            if (await sets.FindSnapshotAsync(setId, cancellationToken) is { IsDeleted: false })
            {
                await sets.AppendAsync(setId, [new FlashCardSetDeleted(setId, guardian.UserId, time.GetUtcNow())], cancellationToken);
            }
        }
    }

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<FlashCardPracticeSnapshot>(FlashCardPracticeId.ForChild(child.UserId).Value, cancellationToken);
}
