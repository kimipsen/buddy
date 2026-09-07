using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ApplyAiSessionDraftEndpoint
{
    public static RouteGroupBuilder MapApplyAiSessionDraft(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions/current/apply", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult, BadRequest<ErrorEnvelope>>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = ApplyAiSessionDraft.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<AiSessionView>>(command, cancellationToken);

            return result switch
            {
                Result<AiSessionView>.Success(var view) => TypedResults.Ok(view),
                Result<AiSessionView>.Forbidden => TypedResults.Forbid(),
                Result<AiSessionView>.Validation(var problem) => TypedResults.BadRequest(problem.ToEnvelope(httpContext)),
                Result<AiSessionView>.NotFound => TypedResults.NotFound(),
            };
        })
        .WithName("ApplyAiSessionDraft");

        return mealplans;
    }
}
