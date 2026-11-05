using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class UpdateChildChimeEndpoint
{
    public static RouteGroupBuilder MapUpdateChildChime(this RouteGroupBuilder children)
    {
        // An unbindable body (missing, or a non-boolean allowChime) never reaches the handler:
        // RequestBindingFailureMiddleware renders it as the 400 ErrorEnvelope declared here.
        children.MapPatch("/{childId:guid}/chime", async Task<Results<Ok<ChildSummary>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateChildChimeRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateChildChime.FromClaims(principal, new UserId(childId), request.AllowChime);
            var result = await bus.InvokeAsync<Result<ChildSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ChildSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ChildSummary>.NotFound => TypedResults.NotFound(),
                Result<ChildSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateChildChimeHandler never produces Forbidden -- collapsed to NotFound since
                // this route declares no other status for it.
                Result<ChildSummary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateChildChime");

        return children;
    }
}

public sealed record UpdateChildChimeRequest(bool AllowChime);
