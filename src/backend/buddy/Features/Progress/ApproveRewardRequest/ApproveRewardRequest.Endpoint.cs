using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Progress;

public static class ApproveRewardRequestEndpoint
{
    public static RouteGroupBuilder MapApproveRewardRequest(this RouteGroupBuilder progress)
    {
        progress.MapPost("/children/{childId:guid}/reward-requests/{requestId:guid}/approve", async Task<Results<Ok<ProgressSummary>, NotFound, ForbidHttpResult, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            Guid requestId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ApproveRewardRequest.FromClaims(principal, new UserId(childId), new RewardRequestId(requestId));
            var outcome = await bus.InvokeAsync<RewardRequestOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .ProducesErrorCode(StatusCodes.Status409Conflict, RewardRequestOutcome.InsufficientStarsCode)
        .ProducesErrorCode(StatusCodes.Status409Conflict, RewardRequestOutcome.AlreadyResolvedCode)
        .WithName("ApproveRewardRequest");

        return progress;
    }
}
