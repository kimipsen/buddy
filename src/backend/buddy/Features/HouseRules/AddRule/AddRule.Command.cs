using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record AddRule(UserId UserId, RuleBookScope Scope, string Title, string Body)
{
    public static AddRule FromClaims(ClaimsPrincipal principal, RuleBookScope scope, AddRuleRequest request) =>
        new(principal.GetRequiredUserId(), scope, FreeText.Normalize(request.Title), RuleContentRules.NormalizeBody(request.Body));
}
