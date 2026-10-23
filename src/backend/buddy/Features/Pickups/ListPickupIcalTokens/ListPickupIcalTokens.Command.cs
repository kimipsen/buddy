using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record ListPickupIcalTokens(UserId UserId)
{
    public static ListPickupIcalTokens FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}

// Never exposes the hash -- just enough for a guardian to recognize which token to revoke.
public sealed record PickupIcalTokenSummary(Guid TokenId, DateTimeOffset IssuedAt);
