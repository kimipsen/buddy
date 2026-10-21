using System.Security.Claims;

using buddy.Common;
using buddy.Common.RateLimiting;
using buddy.Features.Calendars;
using buddy.Features.Users;

using Wolverine;

namespace buddy.Features.Mealplans;

public static class CreateMealFromRecipeEndpoint
{
    // Create-style POST: IdempotencyKeyMiddleware replays the first response for a repeated
    // Idempotency-Key, so a double click can't create the meal twice.
    public static RouteGroupBuilder MapCreateMealFromRecipe(this RouteGroupBuilder mealplans)
    {
        mealplans.MapPost("/children/{childId:guid}/meals/from-recipe", async (
            ClaimsPrincipal principal,
            Guid childId,
            CreateMealFromRecipeRequest request,
            IMessageBus bus,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var command = CreateMealFromRecipe.FromClaims(
                principal, new UserId(childId), request.RecipeId, new Icon(request.Icon ?? ""), new Color(FreeText.Normalize(request.Color)));
            var outcome = await bus.InvokeAsync<MealieOutcome<Meal>>(command, cancellationToken);

            return outcome.Map(MealResponse.FromMeal).ToHttpResult(httpContext);
        })
        .RequireRateLimiting(RateLimitingFeature.MealiePolicy)
        .ProducesMealieErrors()
        .WithName("CreateMealFromRecipe");

        return mealplans;
    }
}

public sealed record CreateMealFromRecipeRequest(Guid RecipeId, string? Icon = null, string? Color = null);
