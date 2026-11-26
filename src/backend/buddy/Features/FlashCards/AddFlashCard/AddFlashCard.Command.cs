using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record AddFlashCard(UserId UserId, FlashCardSetId SetId, string Question, string Answer)
{
    public static AddFlashCard FromClaims(ClaimsPrincipal principal, FlashCardSetId setId, string question, string answer) =>
        new(principal.GetRequiredUserId(), setId, question, answer);
}
