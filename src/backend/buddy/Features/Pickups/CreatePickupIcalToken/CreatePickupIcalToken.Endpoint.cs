using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class CreatePickupIcalTokenEndpoint
{
    public static RouteGroupBuilder MapCreatePickupIcalToken(this RouteGroupBuilder pickups)
    {
        pickups.MapPost("/me/ical-tokens", async Task<Results<Ok<PickupIcalTokenResponse>, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = CreatePickupIcalToken.FromClaims(principal);
            var result = await bus.InvokeAsync<Result<IssuedPickupIcalToken>>(command, cancellationToken);

            return result switch
            {
                Result<IssuedPickupIcalToken>.Success(var issued) => TypedResults.Ok(new PickupIcalTokenResponse(
                    issued.TokenId.Value,
                    issued.Token,
                    $"/pickups/feeds/{issued.FeedId.Value}/ical/{issued.Token}")),
                Result<IssuedPickupIcalToken>.Forbidden => TypedResults.Forbid(),
                // CheckOwnFeed never returns NotFound and the handler has no validation -- there's no
                // NotFound/BadRequest in this route's declared results, so both collapse to Forbid.
                Result<IssuedPickupIcalToken>.NotFound => TypedResults.Forbid(),
                Result<IssuedPickupIcalToken>.Validation => TypedResults.Forbid(),
            };
        })
        .WithName("CreatePickupIcalToken");

        return pickups;
    }
}

// SubscriptionPath is the ready-to-paste feed URL path (relative -- prefix with this API's host).
public sealed record PickupIcalTokenResponse(Guid TokenId, string Token, string SubscriptionPath);
