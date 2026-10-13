using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class SetFeelingsDisplayModeEndpoint
{
    public static RouteGroupBuilder MapSetFeelingsDisplayMode(this RouteGroupBuilder feelings)
    {
        feelings.MapPut("/children/{childId:guid}/display-mode", async Task<Results<Ok<DisplayModeResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            SetFeelingsDisplayModeRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetFeelingsDisplayMode.FromClaims(principal, new UserId(childId), request.DisplayMode);
            var result = await bus.InvokeAsync<Result<DisplayModeResponse>>(command, cancellationToken);

            return result switch
            {
                Result<DisplayModeResponse>.Success(var mode) => TypedResults.Ok(mode),
                Result<DisplayModeResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<DisplayModeResponse>.NotFound => TypedResults.NotFound(),
                Result<DisplayModeResponse>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("SetFeelingsDisplayMode");

        return feelings;
    }
}

public sealed record SetFeelingsDisplayModeRequest(FeelingsDisplayMode DisplayMode);
