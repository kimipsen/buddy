using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record RemoveRule(UserId UserId, RuleBookScope Scope, RuleId RuleId)
{
    public static RemoveRule FromClaims(ClaimsPrincipal principal, RuleBookScope scope, RuleId ruleId) =>
        new(principal.GetRequiredUserId(), scope, ruleId);
}
