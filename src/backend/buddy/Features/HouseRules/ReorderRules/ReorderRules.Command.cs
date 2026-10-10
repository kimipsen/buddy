using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.HouseRules;

public sealed record ReorderRules(UserId UserId, RuleBookScope Scope, ImmutableList<RuleId> NewOrder)
{
    public static ReorderRules FromClaims(ClaimsPrincipal principal, RuleBookScope scope, ReorderRulesRequest request) =>
        new(principal.GetRequiredUserId(), scope, [.. (request.NewOrder ?? []).Select(id => new RuleId(id))]);
}
