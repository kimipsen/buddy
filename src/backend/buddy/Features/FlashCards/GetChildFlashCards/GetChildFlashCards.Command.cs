using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FlashCards;

public sealed record GetChildFlashCards(UserId UserId, UserId ChildId)
{
    public static GetChildFlashCards FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}
