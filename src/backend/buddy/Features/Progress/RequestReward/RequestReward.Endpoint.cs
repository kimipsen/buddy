using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class RequestRewardEndpoint
{
    public static RouteGroupBuilder MapRequestReward(this RouteGroupBuilder progress)
    {
        progress.MapPost("/me/reward-requests", async Task<Results<Ok<ProgressSummary>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            RequestRewardRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<RewardRequestOutcome>(RequestReward.FromClaims(principal, new RewardId(request.RewardId)), cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, RewardRequestOutcome.InsufficientStarsCode)
        .ProducesErrorCode(StatusCodes.Status409Conflict, RewardRequestOutcome.TooManyPendingRequestsCode)
        .WithName("RequestReward");

        return progress;
    }
}

public sealed record RequestRewardRequest(Guid RewardId);
