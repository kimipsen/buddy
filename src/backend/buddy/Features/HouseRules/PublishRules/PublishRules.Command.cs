using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record PublishRules(UserId UserId, RuleBookScope Scope)
{
    public static PublishRules FromClaims(ClaimsPrincipal principal, RuleBookScope scope) =>
        new(principal.GetRequiredUserId(), scope);
}
