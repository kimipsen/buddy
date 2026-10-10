using System.Security.Claims;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class ListRulesEndpoint
{
    public static RouteGroupBuilder MapListRules(this RouteGroupBuilder houseRules)
    {
        houseRules.MapGet("/children/{childId:guid}/rules", (ClaimsPrincipal principal, Guid childId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(ListRules.FromClaims(principal, RuleBookScope.ForChild(new UserId(childId))), bus, cancellationToken))
            .WithName("ListRules");

        houseRules.MapGet("/groups/{groupId:guid}/rules", (ClaimsPrincipal principal, Guid groupId, IMessageBus bus, CancellationToken cancellationToken) =>
            HandleAsync(ListRules.FromClaims(principal, RuleBookScope.ForGroup(new GroupId(groupId))), bus, cancellationToken))
            .WithName("ListRulesForGroup");

        return houseRules;
    }

    private static async Task<Results<Ok<RuleBookResponse>, NotFound>> HandleAsync(
        ListRules command,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<RuleBookResponse>>(command, cancellationToken);

        return result switch
        {
            Result<RuleBookResponse>.Success(var book) => TypedResults.Ok(book),
            Result<RuleBookResponse>.NotFound => TypedResults.NotFound(),
            // Reading needs only the lowest tier and has no input, so the handler never returns
            // Forbidden or Validation; collapsed to NotFound rather than widening Results<...>.
            Result<RuleBookResponse>.Forbidden => TypedResults.NotFound(),
            Result<RuleBookResponse>.Validation => TypedResults.NotFound(),
        };
    }
}
