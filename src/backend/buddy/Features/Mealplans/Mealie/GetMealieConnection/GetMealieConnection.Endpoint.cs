using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

using Microsoft.AspNetCore.Http.HttpResults;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetMealieConnectionEndpoint
{
    public static RouteGroupBuilder MapGetMealieConnection(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/mealie", async Task<Results<Ok<MealieConnectionView>, NotFound, ForbidHttpResult>> (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            CancellationToken cancellationToken) =>
        {
            var result = await bus.InvokeAsync<Result<MealieConnectionView>>(GetMealieConnection.FromClaims(principal, new UserId(childId)), cancellationToken);

            return result switch
            {
                Result<MealieConnectionView>.Success(var view) => TypedResults.Ok(view),
                Result<MealieConnectionView>.Forbidden => TypedResults.Forbid(),
                Result<MealieConnectionView>.NotFound => TypedResults.NotFound(),
                // Never produced: the handler has no validator.
                Result<MealieConnectionView>.Validation => TypedResults.NotFound(),
            };
        })
        .WithName("GetMealieConnection");

        return mealplans;
    }
}
