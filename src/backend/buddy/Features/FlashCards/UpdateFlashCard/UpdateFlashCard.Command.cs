using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record UpdateFlashCard(UserId UserId, FlashCardSetId SetId, FlashCardId CardId, string Question, string Answer)
{
    public static UpdateFlashCard FromClaims(ClaimsPrincipal principal, FlashCardSetId setId, FlashCardId cardId, string question, string answer) =>
        new(principal.GetRequiredUserId(), setId, cardId, question, answer);
}
