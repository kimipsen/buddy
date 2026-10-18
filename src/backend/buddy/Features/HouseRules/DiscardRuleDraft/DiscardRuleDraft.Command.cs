using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record DiscardRuleDraft(UserId UserId, RuleBookScope Scope, RuleId RuleId)
{
    public static DiscardRuleDraft FromClaims(ClaimsPrincipal principal, RuleBookScope scope, RuleId ruleId) =>
        new(principal.GetRequiredUserId(), scope, ruleId);
}
