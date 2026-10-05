using System.Security.Claims;

using buddy.Common;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Babysitters;

public static class UpdateBabysitterEndpoint
{
    public static RouteGroupBuilder MapUpdateBabysitter(this RouteGroupBuilder babysitters)
    {
        babysitters.MapPatch("/me/{babysitterId:guid}", async Task<Results<Ok<BabysitterSummary>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid babysitterId,
            BabysitterRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateBabysitter.FromClaims(principal, new BabysitterId(babysitterId), request.Name, request.ContactInfo ?? "");
            var result = await bus.InvokeAsync<Result<BabysitterSummary>>(command, cancellationToken);

            return result switch
            {
                Result<BabysitterSummary>.Success(var babysitter) => TypedResults.Ok(babysitter),
                Result<BabysitterSummary>.Forbidden => TypedResults.Forbid(),
                Result<BabysitterSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<BabysitterSummary>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateBabysitter");

        return babysitters;
    }
}
