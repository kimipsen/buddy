using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.HouseRules;

public static class GetChildRulesEndpoint
{
    public static RouteGroupBuilder MapGetChildRules(this RouteGroupBuilder houseRules)
    {
        houseRules.MapGet("/children/{childId:guid}", async Task<Results<Ok<ChildRulesResponse>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<ChildRulesResponse>>(
                GetChildRules.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<ChildRulesResponse>.Success(var rules) => TypedResults.Ok(rules),
                Result<ChildRulesResponse>.NotFound => TypedResults.NotFound(),
                // Any tier may read and there's no input, so neither is ever returned; collapsed to
                // NotFound rather than widening Results<...>.
                Result<ChildRulesResponse>.Forbidden => TypedResults.NotFound(),
                Result<ChildRulesResponse>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetChildRules");

        return houseRules;
    }
}
