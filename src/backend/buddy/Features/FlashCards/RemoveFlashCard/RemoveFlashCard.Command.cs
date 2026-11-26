using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record RemoveFlashCard(UserId UserId, FlashCardSetId SetId, FlashCardId CardId)
{
    public static RemoveFlashCard FromClaims(ClaimsPrincipal principal, FlashCardSetId setId, FlashCardId cardId) =>
        new(principal.GetRequiredUserId(), setId, cardId);
}
