using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record RevokePickupIcalToken(UserId UserId, IcalTokenId TokenId)
{
    public static RevokePickupIcalToken FromClaims(ClaimsPrincipal principal, IcalTokenId tokenId) =>
        new(principal.GetRequiredUserId(), tokenId);
}
