using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record PreviewMealieImport(UserId UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static PreviewMealieImport FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), childId, from, to);
}

// The import review for a Mealie date range, plus which recipe each group came from: the commit
// sends it back as entries[].mealieRecipe, so a new meal is created already linked.
public sealed record MealieImportPreview(MealPlanImportPreview Preview, IReadOnlyList<MealieImportRecipe> Recipes);

public sealed record MealieImportRecipe(string Key, MealieRecipeRef Recipe);
