using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class DisconnectMealieEndpoint
{
    public static RouteGroupBuilder MapDisconnectMealie(this RouteGroupBuilder mealplans)
    {
        mealplans.MapDelete("/children/{childId:guid}/mealie", async Task<Results<NoContent, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<Unit>>(DisconnectMealie.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<Unit>.Success => TypedResults.NoContent(),
                Result<Unit>.Forbidden => TypedResults.Forbid(),
                Result<Unit>.NotFound => TypedResults.NotFound(),
                // Never produced: the handler has no validator.
                Result<Unit>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("DisconnectMealie");

        return mealplans;
    }
}
