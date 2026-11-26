using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record DeleteFlashCardSet(UserId UserId, FlashCardSetId SetId)
{
    public static DeleteFlashCardSet FromClaims(ClaimsPrincipal principal, FlashCardSetId setId) => new(principal.GetRequiredUserId(), setId);
}
