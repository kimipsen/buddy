using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class ArchiveWorkLocationEndpoint
{
    public static RouteGroupBuilder MapArchiveWorkLocation(this RouteGroupBuilder workLocations)
    {
        workLocations.MapDelete("/me/locations/{locationId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid locationId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ArchiveWorkLocation.FromClaims(principal, new WorkLocationId(locationId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ArchiveWorkLocationHandler has no input to validate -- there's no BadRequest in
                // this route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ArchiveWorkLocation");

        return workLocations;
    }
}
