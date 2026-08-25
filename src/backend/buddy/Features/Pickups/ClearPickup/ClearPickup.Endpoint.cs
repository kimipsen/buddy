using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Pickups;

public static class ClearPickupEndpoint
{
    public static RouteGroupBuilder MapClearPickup(this RouteGroupBuilder pickups)
    {
        pickups.MapDelete("/children/{childId:guid}/assignments", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            DateOnly date,
            PickupSlot slot,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ClearPickup.FromClaims(principal, new UserId(childId), date, slot);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ClearPickupHandler never produces Validation -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound like the others.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ClearPickup");

        return pickups;
    }
}
