using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record EditRule(UserId UserId, RuleBookScope Scope, RuleId RuleId, string Title, string Body, bool RequireReacknowledgement)
{
    public static EditRule FromClaims(ClaimsPrincipal principal, RuleBookScope scope, RuleId ruleId, EditRuleRequest request) =>
        new(
            principal.GetRequiredUserId(),
            scope,
            ruleId,
            FreeText.Normalize(request.Title),
            RuleContentRules.NormalizeBody(request.Body),
            request.RequireReacknowledgement);

    public RuleContent Content => new(Title, Body);
}
