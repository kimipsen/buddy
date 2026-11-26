using buddy.Common.Erasure;
using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// A child's practice (enabled sets, tiers, every finished game with its per-card results) is theirs
// alone, keyed by their UserId: erasing the child deletes it. See gdpr-data-protection.md.
//
// Guardian-owned sets are not handled yet: the library slices that create them come with the rule
// for erasing their content (hand to the heir, else soft-delete).
public sealed class FlashCardsPersonalDataEraser(IFlashCardsStore store) : IPersonalDataEraser
{
    public Type Store => typeof(IFlashCardsStore);

    public Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        store.DeleteStreamAsync<FlashCardPracticeSnapshot>(FlashCardPracticeId.ForChild(child.UserId).Value, cancellationToken);
}
