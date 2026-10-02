using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.WorkLocations;

public static class SetWorkLocationOverridesEndpoint
{
    public static RouteGroupBuilder MapSetWorkLocationOverrides(this RouteGroupBuilder workLocations)
    {
        workLocations.MapPut("/me/overrides", async Task<Results<Ok<IReadOnlyCollection<WorkDay>>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            SetWorkLocationOverridesRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetWorkLocationOverrides.FromClaims(
                principal,
                request.From,
                request.To,
                request.LocationId is { } locationId ? new WorkLocationId(locationId) : null);
            var result = await bus.InvokeAsync<Result<IReadOnlyCollection<WorkDay>>>(command, cancellationToken);

            return result switch
            {
                Result<IReadOnlyCollection<WorkDay>>.Success(var days) => TypedResults.Ok(days),
                Result<IReadOnlyCollection<WorkDay>>.Forbidden => TypedResults.Forbid(),
                Result<IReadOnlyCollection<WorkDay>>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<IReadOnlyCollection<WorkDay>>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SetWorkLocationOverrides");

        return workLocations;
    }
}

public sealed record SetWorkLocationOverridesRequest(DateOnly From, DateOnly To, Guid? LocationId = null);
