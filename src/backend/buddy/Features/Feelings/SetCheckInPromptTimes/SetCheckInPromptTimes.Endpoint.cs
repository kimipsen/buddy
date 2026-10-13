using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Feelings;

public static class SetCheckInPromptTimesEndpoint
{
    public static RouteGroupBuilder MapSetCheckInPromptTimes(this RouteGroupBuilder feelings)
    {
        feelings.MapPut("/children/{childId:guid}/prompt-times", async Task<Results<Ok<PromptTimesResponse>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            SetCheckInPromptTimesRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SetCheckInPromptTimes.FromClaims(principal, new UserId(childId), request.Times);
            var result = await bus.InvokeAsync<Result<PromptTimesResponse>>(command, cancellationToken);

            return result switch
            {
                Result<PromptTimesResponse>.Success(var times) => TypedResults.Ok(times),
                Result<PromptTimesResponse>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<PromptTimesResponse>.NotFound => TypedResults.NotFound(),
                Result<PromptTimesResponse>.Forbidden => TypedResults.Forbid(),
            };
        })
        .WithName("SetCheckInPromptTimes");

        return feelings;
    }
}

public sealed record SetCheckInPromptTimesRequest(IReadOnlyList<TimeOnly> Times);
