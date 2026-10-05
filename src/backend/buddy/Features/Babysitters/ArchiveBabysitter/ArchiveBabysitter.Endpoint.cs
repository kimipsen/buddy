using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Babysitters;

public static class ArchiveBabysitterEndpoint
{
    public static RouteGroupBuilder MapArchiveBabysitter(this RouteGroupBuilder babysitters)
    {
        babysitters.MapDelete("/me/{babysitterId:guid}", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid babysitterId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = ArchiveBabysitter.FromClaims(principal, new BabysitterId(babysitterId));
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // ArchiveBabysitterHandler has no input to validate -- there's no BadRequest in this
                // route's declared results, so this collapses to NotFound.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("ArchiveBabysitter");

        return babysitters;
    }
}
