using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Guardians;

public static class UpdateChildHomeworkChunksEndpoint
{
    public static RouteGroupBuilder MapUpdateChildHomeworkChunks(this RouteGroupBuilder children)
    {
        // An unbindable body (missing, or non-integer minutes) never reaches the handler:
        // RequestBindingFailureMiddleware renders it as the 400 ErrorEnvelope declared here.
        children.MapPatch("/{childId:guid}/homework-chunks", async Task<Results<Ok<ChildSummary>, BadRequest<ErrorEnvelope>, NotFound>> (
            ClaimsPrincipal principal,
            Guid childId,
            UpdateChildHomeworkChunksRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = UpdateChildHomeworkChunks.FromClaims(
                principal, new UserId(childId), new HomeworkChunks(request.FocusMinutes, request.BreakMinutes));
            var result = await bus.InvokeAsync<Result<ChildSummary>>(command, cancellationToken);

            return result switch
            {
                Result<ChildSummary>.Success(var summary) => TypedResults.Ok(summary),
                Result<ChildSummary>.NotFound => TypedResults.NotFound(),
                Result<ChildSummary>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                // UpdateChildHomeworkChunksHandler never produces Forbidden -- collapsed to NotFound
                // since this route declares no other status for it.
                Result<ChildSummary>.Forbidden => TypedResults.NotFound(),
            };
        })
        .WithName("UpdateChildHomeworkChunks");

        return children;
    }
}

public sealed record UpdateChildHomeworkChunksRequest(int FocusMinutes, int BreakMinutes);
