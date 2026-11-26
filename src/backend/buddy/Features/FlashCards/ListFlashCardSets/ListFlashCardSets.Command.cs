using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record ListFlashCardSets(UserId UserId)
{
    public static ListFlashCardSets FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
