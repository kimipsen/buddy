using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class RevokePickupIcalTokenEndpoint
{
    public static RouteGroupBuilder MapRevokePickupIcalToken(this RouteGroupBuilder pickups)
    {
        pickups.MapDelete("/me/ical-tokens/{tokenId:guid}", async Task<Results<NoContent, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid tokenId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = RevokePickupIcalToken.FromClaims(principal, new IcalTokenId(tokenId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                // CheckOwnFeed never returns NotFound and the handler has no validation -- there's no
                // NotFound/BadRequest in this route's declared results, so both collapse to Forbid.
                Result<Unit>.NotFound => TypedResults.Forbid(),
                Result<Unit>.Validation => TypedResults.Forbid(),
            };
        })
        .WithName("RevokePickupIcalToken");

        return pickups;
    }
}
