using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// Id is set to keep an existing tier (its id is what finished games refer to); null makes a new one.
public sealed record FlashCardTierInput(FlashCardTierId? Id, int CardCount, int Stars);

// A full replace of the set's tiers for this child.
public sealed record EnableFlashCardSet(UserId UserId, UserId ChildId, FlashCardSetId SetId, IReadOnlyList<FlashCardTierInput> Tiers)
{
    public static EnableFlashCardSet FromClaims(ClaimsPrincipal principal, UserId childId, FlashCardSetId setId, IReadOnlyList<FlashCardTierInput> tiers) =>
        new(principal.GetRequiredUserId(), childId, setId, tiers);
}
