using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class EditRuleEndpoint
{
    public static RouteGroupBuilder MapEditRule(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPut("/children/{childId:guid}/rules/{ruleId:guid}", (ClaimsPrincipal principal, Guid childId, Guid ruleId, EditRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(EditRule.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), new RuleId(ruleId), request), bus, httpContext, cancellationToken))
            .WithName("EditRule");

        houseRules.MapPut("/groups/{groupId:guid}/rules/{ruleId:guid}", (ClaimsPrincipal principal, Guid groupId, Guid ruleId, EditRuleRequest request, IMessageBus bus, HttpContext httpContext, CancellationToken cancellationToken) =>
            HandleAsync(EditRule.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), new RuleId(ruleId), request), bus, httpContext, cancellationToken))
            .WithName("EditRuleForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> HandleAsync(
        EditRule command,
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

// RequireReacknowledgement defaults to true: only a guardian who ticks "this is a small fix" keeps
// the children's acknowledgements (house-rules.md, Question 3).
public sealed record EditRuleRequest(string? Title, string? Body = null, bool RequireReacknowledgement = true);
