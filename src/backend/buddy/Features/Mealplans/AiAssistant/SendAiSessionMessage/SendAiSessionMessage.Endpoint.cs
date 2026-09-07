using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class SendAiSessionMessageEndpoint
{
    public static RouteGroupBuilder MapSendAiSessionMessage(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions/current/messages", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            SendAiSessionMessageRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = SendAiSessionMessage.FromClaims(principal, new UserId(childId), request.Text);
            var result = await bus.InvokeAsync<Result<AiSessionView>>(command, cancellationToken);

            return result switch
            {
                Result<AiSessionView>.Success(var view) => TypedResults.Ok(view),
                Result<AiSessionView>.Forbidden => TypedResults.Forbid(),
                Result<AiSessionView>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiSessionView>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("SendAiSessionMessage");

        return mealplans;
    }
}

public sealed record SendAiSessionMessageRequest(string Text);
