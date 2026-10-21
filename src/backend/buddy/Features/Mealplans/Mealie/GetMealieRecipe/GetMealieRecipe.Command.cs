using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record GetMealieRecipe(UserId UserId, UserId ChildId, Guid RecipeId)
{
    public static GetMealieRecipe FromClaims(ClaimsPrincipal principal, UserId childId, Guid recipeId) =>
        new(principal.GetRequiredUserId(), childId, recipeId);
}

public sealed record MealieRecipeStep(string Title, string Text);

// What the recipe panel shows. MealieUrl opens the recipe in the family's own Mealie; OriginalUrl
// is where Mealie scraped it from, if anywhere.
public sealed record MealieRecipeDetails(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string TotalTime,
    string RecipeYield,
    bool HasImage,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<MealieRecipeStep> Steps,
    string OriginalUrl,
    string MealieUrl,
    MealId? LinkedMealId);
