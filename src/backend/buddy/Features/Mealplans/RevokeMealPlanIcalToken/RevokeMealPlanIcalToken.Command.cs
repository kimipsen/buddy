using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record RevokeMealPlanIcalToken(UserId? UserId, UserId ChildId, IcalTokenId TokenId)
{
    public static RevokeMealPlanIcalToken FromClaims(ClaimsPrincipal principal, UserId childId, IcalTokenId tokenId) =>
        new(principal.GetUserId(), childId, tokenId);
}
