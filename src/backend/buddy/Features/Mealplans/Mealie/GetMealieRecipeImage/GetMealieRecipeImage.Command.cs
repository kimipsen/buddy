using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record GetMealieRecipeImage(UserId UserId, UserId ChildId, Guid RecipeId)
{
    public static GetMealieRecipeImage FromClaims(ClaimsPrincipal principal, UserId childId, Guid recipeId) =>
        new(principal.GetRequiredUserId(), childId, recipeId);
}
