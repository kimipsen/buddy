using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record ListRules(UserId UserId, RuleBookScope Scope)
{
    public static ListRules FromClaims(ClaimsPrincipal principal, RuleBookScope scope) =>
        new(principal.GetRequiredUserId(), scope);
}
