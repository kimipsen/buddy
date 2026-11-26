using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record ListMyFlashCardSets(UserId ChildId)
{
    public static ListMyFlashCardSets FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
