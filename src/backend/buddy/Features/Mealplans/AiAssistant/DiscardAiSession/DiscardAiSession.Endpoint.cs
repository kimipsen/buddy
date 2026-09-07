using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class DiscardAiSessionEndpoint
{
    public static RouteGroupBuilder MapDiscardAiSession(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/ai/sessions/current/discard", async Task<Results<Ok<AiSessionView>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var command = DiscardAiSession.FromClaims(principal, new UserId(childId));
            var result = await bus.InvokeAsync<Result<AiSessionView>>(command, cancellationToken);

            return result switch
            {
                Result<AiSessionView>.Success(var view) => TypedResults.Ok(view),
                Result<AiSessionView>.Forbidden => TypedResults.Forbid(),
                Result<AiSessionView>.NotFound => TypedResults.NotFound(),
                // DiscardAiSessionHandler never validates -- there's no BadRequest in this route's
                // declared results, so this collapses to NotFound like the others.
                Result<AiSessionView>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DiscardAiSession");

        return mealplans;
    }
}
