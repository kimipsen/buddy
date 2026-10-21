using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class SearchMealieRecipesEndpoint
{
    public static RouteGroupBuilder MapSearchMealieRecipes(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/mealie/recipes", async (
            ClaimsPrincipal principal,
            Guid childId,
            string? search,
            int? page,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var query = SearchMealieRecipes.FromClaims(principal, new UserId(childId), FreeText.Normalize(search), page ?? 1);
            var outcome = await bus.InvokeAsync<MealieOutcome<MealieRecipePage>>(query, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("SearchMealieRecipes");

        return mealplans;
    }
}
