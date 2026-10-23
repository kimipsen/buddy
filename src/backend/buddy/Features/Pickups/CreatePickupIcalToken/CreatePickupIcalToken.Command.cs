using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record CreatePickupIcalToken(UserId UserId)
{
    public static CreatePickupIcalToken FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}

// Token is the plaintext subscription secret -- returned exactly once, on creation, and never again.
public sealed record IssuedPickupIcalToken(IcalTokenId TokenId, string Token, PickupFeedId FeedId);
