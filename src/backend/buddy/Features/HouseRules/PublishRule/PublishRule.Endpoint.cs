using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class PublishRuleEndpoint
{
    // POST: publishing is an action, not a replacement of the rule. It's still idempotent -- a rule
    // with no draft appends nothing (house-rules-publishing.md, Question 3).
    public static RouteGroupBuilder MapPublishRule(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPost("/children/{childId:guid}/rules/{ruleId:guid}/publish", (ClaimsPrincipal principal, Guid childId, Guid ruleId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(PublishRule.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId)), new RuleId(ruleId)), bus, cancellationToken))
            .WithName("PublishRule");

        houseRules.MapPost("/groups/{groupId:guid}/rules/{ruleId:guid}/publish", (ClaimsPrincipal principal, Guid groupId, Guid ruleId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(PublishRule.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId)), new RuleId(ruleId)), bus, cancellationToken))
            .WithName("PublishRuleForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound, ForbidHttpResult>> HandleAsync(
        PublishRule command,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<RuleBookResponse>>(command, cancellationToken);

        return result switch
        {
            Result<RuleBookResponse>.Success(var book) => TypedResults.Ok(book),
            Result<RuleBookResponse>.Forbidden => TypedResults.Forbid(),
            Result<RuleBookResponse>.NotFound => TypedResults.NotFound(),
            // PublishRule has no input to validate, so the handler never returns Validation;
            // collapsed to NotFound rather than widening Results<...>.
            Result<RuleBookResponse>.Validation => TypedResults.NotFound(),
        };
    }
}
