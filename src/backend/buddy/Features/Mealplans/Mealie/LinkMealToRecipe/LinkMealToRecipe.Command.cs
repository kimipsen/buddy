using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record LinkMealToRecipe(UserId UserId, UserId ChildId, MealId MealId, Guid RecipeId)
{
    public static LinkMealToRecipe FromClaims(ClaimsPrincipal principal, UserId childId, MealId mealId, Guid recipeId) =>
        new(principal.GetRequiredUserId(), childId, mealId, recipeId);
}

public sealed record UnlinkMealRecipe(UserId UserId, UserId ChildId, MealId MealId)
{
    public static UnlinkMealRecipe FromClaims(ClaimsPrincipal principal, UserId childId, MealId mealId) =>
        new(principal.GetRequiredUserId(), childId, mealId);
}
