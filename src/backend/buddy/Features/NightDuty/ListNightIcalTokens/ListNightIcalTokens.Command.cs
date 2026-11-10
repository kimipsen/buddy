using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// The caller's own tokens for one child; a co-guardian's tokens for the same child are theirs alone.
public sealed record ListNightIcalTokens(UserId UserId, UserId ChildId)
{
    public static ListNightIcalTokens FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}

// Never exposes the hash -- just enough for a guardian to recognize which token to revoke.
public sealed record NightIcalTokenSummary(Guid TokenId, DateTimeOffset IssuedAt);
