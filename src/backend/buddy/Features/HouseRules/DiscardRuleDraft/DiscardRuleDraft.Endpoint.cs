using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class DiscardRuleDraftEndpoint
{
    public static RouteGroupBuilder MapDiscardRuleDraft(this RouteGroupBuilder houseRules)
    {
        houseRules.MapDelete("/children/{childId:guid}/rules/{ruleId:guid}/draft", (ClaimsPrincipal principal, Guid childId, Guid ruleId, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(DiscardRuleDraft.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), new RuleId(ruleId)), bus, httpContext, cancellationToken))
            .WithName("DiscardRuleDraft");

        houseRules.MapDelete("/groups/{groupId:guid}/rules/{ruleId:guid}/draft", (ClaimsPrincipal principal, Guid groupId, Guid ruleId, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(DiscardRuleDraft.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), new RuleId(ruleId)), bus, httpContext, cancellationToken))
            .WithName("DiscardRuleDraftForGroup");

        return houseRules;
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> HandleAsync(
        DiscardRuleDraft command,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

        return result switch
        {
            Result<Unit>.Success => TypedResults.NoContent(),
            Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            Result<Unit>.Forbidden => TypedResults.Forbid(),
            Result<Unit>.NotFound => TypedResults.NotFound(),
        };
    }
}
