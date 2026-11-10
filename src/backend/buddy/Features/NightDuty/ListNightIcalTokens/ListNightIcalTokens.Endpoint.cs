using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.NightDuty;

public static class ListNightIcalTokensEndpoint
{
    public static RouteGroupBuilder MapListNightIcalTokens(this RouteGroupBuilder nightDuty)
    {
        nightDuty.MapGet("/children/{childId:guid}/ical-tokens", async Task<Results<Ok<IReadOnlyCollection<NightIcalTokenSummary>>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListNightIcalTokens.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<NightIcalTokenSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<NightIcalTokenSummary>>.Success(var tokens) => TypedResults.Ok(tokens),
                Result<IReadOnlyCollection<NightIcalTokenSummary>>.NotFound => TypedResults.NotFound(),
                Result<IReadOnlyCollection<NightIcalTokenSummary>>.Forbidden => TypedResults.Forbid(),
                // The query has no input to validate -- collapsed to NotFound since this route
                // declares no 400.
                Result<IReadOnlyCollection<NightIcalTokenSummary>>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ListNightIcalTokens");

        return nightDuty;
    }
}
