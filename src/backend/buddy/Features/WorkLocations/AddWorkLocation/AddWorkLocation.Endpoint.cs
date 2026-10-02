using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class AddWorkLocationEndpoint
{
    public static RouteGroupBuilder MapAddWorkLocation(this RouteGroupBuilder workLocations)
    {
        workLocations.MapPost("/me/locations", async Task<Results<Ok<WorkLocationSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            WorkLocationRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AddWorkLocation.FromClaims(principal, request.Name ?? "", request.Icon ?? "", request.Color ?? "");
            var result = await bus.InvokeAsync<Result<WorkLocationSummary>>(command, cancellationToken);

            return result switch
            {
                Result<WorkLocationSummary>.Success(var location) => TypedResults.Ok(location),
                Result<WorkLocationSummary>.Forbidden => TypedResults.Forbid(),
                Result<WorkLocationSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<WorkLocationSummary>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AddWorkLocation");

        return workLocations;
    }
}

// Shared by AddWorkLocation and UpdateWorkLocation.
public sealed record WorkLocationRequest(string? Name, string? Icon, string? Color);
