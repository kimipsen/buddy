using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record GetFlashCardDeck(UserId ChildId, FlashCardSetId SetId, FlashCardTierId TierId)
{
    public static GetFlashCardDeck FromClaims(ClaimsPrincipal principal, FlashCardSetId setId, FlashCardTierId tierId) =>
        new(principal.GetRequiredUserId(), setId, tierId);
}
