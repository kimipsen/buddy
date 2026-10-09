using System.Security.Claims;

using buddy.Common;
using buddy.Common.OpenApi;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class SendAiSessionMessageEndpoint
{
    public static RouteGroupBuilder MapSendAiSessionMessage(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions/current/messages", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>, Conflict<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            SendAiSessionMessageRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SendAiSessionMessage.FromClaims(principal, new UserId(childId), request.Text);
            var outcome = await bus.InvokeAsync<AiSessionOutcome>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.AiAssistantPolicy)
        .ProducesErrorCode(StatusCodes.Status409Conflict, AiSessionOutcome.DataSharingNotAcknowledgedCode)
        .WithName("SendAiSessionMessage");

        return mealplans;
    }
}

public sealed record SendAiSessionMessageRequest(string Text);
