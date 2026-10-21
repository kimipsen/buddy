using System.Security.Claims;

using buddy.Common.RateLimiting;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class AddMealPlanToMealieShoppingListEndpoint
{
    // Create-style POST: a repeated Idempotency-Key replays the first answer and doesn't call Mealie
    // again, which would double every quantity on the list.
    public static RouteGroupBuilder MapAddMealPlanToMealieShoppingList(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/mealie/shopping-lists/{listId:guid}/recipes", async (
            ClaimsPrincipal principal,
            Guid childId,
            Guid listId,
            AddMealPlanToMealieShoppingListRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = AddMealPlanToMealieShoppingList.FromClaims(principal, new UserId(childId), listId, request.From, request.To, request.Slots);
            var outcome = await bus.InvokeAsync<MealieOutcome<AddedToShoppingList>>(command, cancellationToken);

            return outcome.ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("AddMealPlanToMealieShoppingList");

        return mealplans;
    }
}

public sealed record AddMealPlanToMealieShoppingListRequest(DateOnly From, DateOnly To, IReadOnlyCollection<MealSlot> Slots);
