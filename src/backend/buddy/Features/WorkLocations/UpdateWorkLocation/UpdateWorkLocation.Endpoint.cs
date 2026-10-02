using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class UpdateWorkLocationEndpoint
{
    public static RouteGroupBuilder MapUpdateWorkLocation(this RouteGroupBuilder workLocations)
    {
        workLocations.MapPatch("/me/locations/{locationId:guid}", async Task<Results<Ok<WorkLocationSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid locationId,
            WorkLocationRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateWorkLocation.FromClaims(principal, new WorkLocationId(locationId), request.Name ?? "", request.Icon ?? "", request.Color ?? "");
            var result = await bus.InvokeAsync<Result<WorkLocationSummary>>(command, cancellationToken);

            return result switch
            {
                Result<WorkLocationSummary>.Success(var location) => TypedResults.Ok(location),
                Result<WorkLocationSummary>.Forbidden => TypedResults.Forbid(),
                Result<WorkLocationSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<WorkLocationSummary>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateWorkLocation");

        return workLocations;
    }
}
