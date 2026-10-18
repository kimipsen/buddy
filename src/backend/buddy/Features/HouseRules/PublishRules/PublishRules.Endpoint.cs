using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class PublishRulesEndpoint
{
    // ".../rules/publish" doesn't clash with ".../rules/{ruleId}": the :guid constraint keeps them
    // apart, as it does for ".../rules/order".
    public static RouteGroupBuilder MapPublishRules(this RouteGroupBuilder houseRules)
    {
        houseRules.MapPost("/children/{childId:guid}/rules/publish", (ClaimsPrincipal principal, Guid childId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(PublishRules.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId))), bus, cancellationToken))
            .WithName("PublishRules");

        houseRules.MapPost("/groups/{groupId:guid}/rules/publish", (ClaimsPrincipal principal, Guid groupId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(PublishRules.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId))), bus, cancellationToken))
            .WithName("PublishRulesForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound, ForbidHttpResult>> HandleAsync(
        PublishRules command,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<RuleBookResponse>>(command, cancellationToken);

        return result switch
        {
            Result<RuleBookResponse>.Success(var book) => TypedResults.Ok(book),
            Result<RuleBookResponse>.Forbidden => TypedResults.Forbid(),
            Result<RuleBookResponse>.NotFound => TypedResults.NotFound(),
            // PublishRules has no input to validate, so the handler never returns Validation;
            // collapsed to NotFound rather than widening Results<...>.
            Result<RuleBookResponse>.Validation => TypedResults.NotFound(),
        };
    }
}
