using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class RemoveRuleEndpoint
{
    public static RouteGroupBuilder MapRemoveRule(this RouteGroupBuilder houseRules)
    {
        houseRules.MapDelete("/children/{childId:guid}/rules/{ruleId:guid}", (ClaimsPrincipal principal, Guid childId, Guid ruleId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(RemoveRule.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), new RuleId(ruleId)), bus, cancellationToken))
            .WithName("RemoveRule");

        houseRules.MapDelete("/groups/{groupId:guid}/rules/{ruleId:guid}", (ClaimsPrincipal principal, Guid groupId, Guid ruleId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(RemoveRule.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), new RuleId(ruleId)), bus, cancellationToken))
            .WithName("RemoveRuleForGroup");

        return houseRules;
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> HandleAsync(
        RemoveRule command,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

        return result switch
        {
            Result<Unit>.Success => TypedResults.NoContent(),
            Result<Unit>.Forbidden => TypedResults.Forbid(),
            Result<Unit>.NotFound => TypedResults.NotFound(),
            // RemoveRule has no input to validate, so the handler never returns Validation;
            // collapsed to NotFound rather than widening Results<...>.
            Result<Unit>.Validation => TypedResults.NotFound(),
        };
    }
}
