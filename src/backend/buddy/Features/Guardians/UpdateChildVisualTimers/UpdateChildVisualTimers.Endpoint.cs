using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class UpdateChildVisualTimersEndpoint
{
    public static RouteGroupBuilder MapUpdateChildVisualTimers(this RouteGroupBuilder children)
    {
        // An unbindable body (missing, or a non-boolean showVisualTimers) never reaches the handler:
        // RequestBindingFailureMiddleware renders it as the 400 ErrorEnvelope declared here.
        children.MapPatch("/{childId:guid}/visual-timers", async Task<Results<Ok<ChildSummary>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateChildVisualTimersRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateChildVisualTimers.FromClaims(principal, new UserId(childId), request.ShowVisualTimers);
            var result = await bus.InvokeAsync<Result<ChildSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ChildSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ChildSummary>.NotFound => TypedResults.NotFound(),
                Result<ChildSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateChildVisualTimersHandler never produces Forbidden -- collapsed to NotFound
                // since this route declares no other status for it.
                Result<ChildSummary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateChildVisualTimers");

        return children;
    }
}

public sealed record UpdateChildVisualTimersRequest(bool ShowVisualTimers);
