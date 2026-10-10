using System.Security.Claims;

using buddy.Common.Validation;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// ChildId is whose acknowledgement this is. A child leaves it out (or names themself); a guardian
// acknowledging on behalf of a child who can't read yet must name the child.
public sealed record AcknowledgeRule(UserId UserId, RuleBookScope Scope, RuleId RuleId, int Revision, UserId? ChildId)
{
    public const string RevisionChangedCode = "house_rule_revision_changed";

    public static AcknowledgeRule FromClaims(ClaimsPrincipal principal, RuleBookScope scope, RuleId ruleId, AcknowledgeRuleRequest request) =>
        new(principal.GetRequiredUserId(), scope, ruleId, request.Revision, request.ChildId is { } childId ? new UserId(childId) : null);
}

// A Result<Unit> plus RevisionChanged: the rule was edited (in a way that needs re-acknowledging)
// after the revision the child read, so the client must reload and show the new text first.
public union AcknowledgeRuleOutcome(
    AcknowledgeRuleOutcome.Success,
    AcknowledgeRuleOutcome.NotFound,
    AcknowledgeRuleOutcome.Forbidden,
    AcknowledgeRuleOutcome.Validation,
    AcknowledgeRuleOutcome.RevisionChanged)
{
    public sealed record Success;
    public sealed record NotFound;
    public sealed record Forbidden;
    public sealed record Validation(ValidationProblem Problem);
    public sealed record RevisionChanged(int CurrentRevision);
}
