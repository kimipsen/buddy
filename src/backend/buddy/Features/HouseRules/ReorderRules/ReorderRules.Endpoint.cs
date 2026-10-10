using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class ReorderRulesEndpoint
{
    public static RouteGroupBuilder MapReorderRules(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPut("/children/{childId:guid}/rules/order", (ClaimsPrincipal principal, Guid childId, ReorderRulesRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(ReorderRules.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), request), bus, httpContext, cancellationToken))
            .WithName("ReorderRules");

        houseRules.MapPut("/groups/{groupId:guid}/rules/order", (ClaimsPrincipal principal, Guid groupId, ReorderRulesRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(ReorderRules.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), request), bus, httpContext, cancellationToken))
            .WithName("ReorderRulesForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> HandleAsync(
        ReorderRules command,
        IMessageBus bus,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<RuleBookResponse>>(command, cancellationToken);

        return result switch
        {
            Result<RuleBookResponse>.Success(var book) => TypedResults.Ok(book),
            Result<RuleBookResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
            Result<RuleBookResponse>.Forbidden => TypedResults.Forbid(),
            Result<RuleBookResponse>.NotFound => TypedResults.NotFound(),
        };
    }
}

public sealed record ReorderRulesRequest(ImmutableList<Guid>? NewOrder);
