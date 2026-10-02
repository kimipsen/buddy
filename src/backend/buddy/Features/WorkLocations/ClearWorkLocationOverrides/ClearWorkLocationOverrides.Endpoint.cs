using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class ClearWorkLocationOverridesEndpoint
{
    public static RouteGroupBuilder MapClearWorkLocationOverrides(this RouteGroupBuilder workLocations)
    {
        workLocations.MapDelete("/me/overrides", async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            DateOnly from,
            DateOnly to,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ClearWorkLocationOverrides.FromClaims(principal, from, to);
            var result = await bus.InvokeAsync<Result<Unit>>(command, cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<Unit>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ClearWorkLocationOverrides");

        return workLocations;
    }
}
