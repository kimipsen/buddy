using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Babysitters;

public static class AddBabysitterEndpoint
{
    public static RouteGroupBuilder MapAddBabysitter(this RouteGroupBuilder babysitters)
    {
        babysitters.MapPost("/me", async Task<Results<Ok<BabysitterSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            BabysitterRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AddBabysitter.FromClaims(principal, request.Name, request.ContactInfo ?? "");
            var result = await bus.InvokeAsync<Result<BabysitterSummary>>(command, cancellationToken);

            return result switch
            {
                Result<BabysitterSummary>.Success(var babysitter) => TypedResults.Ok(babysitter),
                Result<BabysitterSummary>.Forbidden => TypedResults.Forbid(),
                Result<BabysitterSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<BabysitterSummary>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("AddBabysitter");

        return babysitters;
    }
}

// Shared by AddBabysitter and UpdateBabysitter. ContactInfo is optional on the way in; it always
// comes back, "" meaning none.
public sealed record BabysitterRequest(string Name, string? ContactInfo = null);
