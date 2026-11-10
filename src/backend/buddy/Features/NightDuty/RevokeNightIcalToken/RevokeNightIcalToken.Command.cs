using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public sealed record RevokeNightIcalToken(UserId UserId, UserId ChildId, IcalTokenId TokenId)
{
    public static RevokeNightIcalToken FromClaims(ClaimsPrincipal principal, UserId childId, IcalTokenId tokenId) =>
        new(principal.GetRequiredUserId(), childId, tokenId);
}
