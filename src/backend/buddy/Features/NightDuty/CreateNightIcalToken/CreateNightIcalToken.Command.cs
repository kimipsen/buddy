using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public sealed record CreateNightIcalToken(UserId UserId, UserId ChildId)
{
    public static CreateNightIcalToken FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}

// Token is the plaintext subscription secret -- returned exactly once, on creation, and never again.
public sealed record IssuedNightIcalToken(IcalTokenId TokenId, string Token, NightFeedId FeedId, DateTimeOffset IssuedAt);
