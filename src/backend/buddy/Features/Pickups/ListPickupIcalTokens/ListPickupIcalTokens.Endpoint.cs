using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class ListPickupIcalTokensEndpoint
{
    public static RouteGroupBuilder MapListPickupIcalTokens(this RouteGroupBuilder pickups)
    {
        pickups.MapGet("/me/ical-tokens", async Task<Results<Ok<IReadOnlyCollection<PickupIcalTokenSummary>>, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var query = ListPickupIcalTokens.FromClaims(principal);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<PickupIcalTokenSummary>>>(query, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<PickupIcalTokenSummary>>.Success(var tokens) => TypedResults.Ok(tokens),
                Result<IReadOnlyCollection<PickupIcalTokenSummary>>.Forbidden => TypedResults.Forbid(),
                // CheckOwnFeed never returns NotFound and the handler has no validation -- there's no
                // NotFound/BadRequest in this route's declared results, so both collapse to Forbid.
                Result<IReadOnlyCollection<PickupIcalTokenSummary>>.NotFound => TypedResults.Forbid(),
                Result<IReadOnlyCollection<PickupIcalTokenSummary>>.Validation => TypedResults.Forbid(),
            };
        })
        .WithName("ListPickupIcalTokens");

        return pickups;
    }
}
