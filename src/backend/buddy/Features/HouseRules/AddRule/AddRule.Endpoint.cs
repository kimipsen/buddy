using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class AddRuleEndpoint
{
    // One command, two routes: a child's personal book and a household group's book build the same
    // AddRule with a different RuleBookScope (the dual-route shape mealplans use).
    public static RouteGroupBuilder MapAddRule(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPost("/children/{childId:guid}/rules", (ClaimsPrincipal principal, Guid childId, AddRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(AddRule.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), request), bus, httpContext, cancellationToken))
            .WithName("AddRule");

        houseRules.MapPost("/groups/{groupId:guid}/rules", (ClaimsPrincipal principal, Guid groupId, AddRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(AddRule.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), request), bus, httpContext, cancellationToken))
            .WithName("AddRuleForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> HandleAsync(
        AddRule command,
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

public sealed record AddRuleRequest(string? Title, string? Body = null);
