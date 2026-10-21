using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class GetMealieRecipeEndpoint
{
    public static RouteGroupBuilder MapGetMealieRecipe(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/mealie/recipes/{recipeId:guid}", async (
            ClaimsPrincipal principal,
            Guid childId,
            Guid recipeId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<MealieOutcome<MealieRecipeDetails>>(GetMealieRecipe.FromClaims(principal, new UserId(childId), recipeId), cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("GetMealieRecipe");

        return mealplans;
    }
}
