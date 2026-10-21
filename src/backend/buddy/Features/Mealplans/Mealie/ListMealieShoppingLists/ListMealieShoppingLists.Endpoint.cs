using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class ListMealieShoppingListsEndpoint
{
    public static RouteGroupBuilder MapListMealieShoppingLists(this RouteGroupBuilder mealplans)
    {
        mealplans.MapGet("/children/{childId:guid}/mealie/shopping-lists", async (
            ClaimsPrincipal principal,
            Guid childId,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var outcome = await bus.InvokeAsync<MealieOutcome<IReadOnlyList<MealieShoppingList>>>(ListMealieShoppingLists.FromClaims(principal, new UserId(childId)), cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("ListMealieShoppingLists");

        return mealplans;
    }
}
