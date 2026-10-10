using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class CancelRewardRequestEndpoint
{
    public static RouteGroupBuilder MapCancelRewardRequest(this RouteGroupBuilder progress)
    {
        progress.MapPost("/me/reward-requests/{requestId:guid}/cancel", async Task<Results<Ok<ProgressSummary>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid requestId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<RewardRequestOutcome>(CancelRewardRequest.FromClaims(principal, new RewardRequestId(requestId)), cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, RewardRequestOutcome.AlreadyResolvedCode)
        .WithName("CancelRewardRequest");

        return progress;
    }
}
