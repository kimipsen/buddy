using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record SearchMealieRecipes(UserId UserId, UserId ChildId, string Search, int Page)
{
    public static SearchMealieRecipes FromClaims(ClaimsPrincipal principal, UserId childId, string search, int page) =>
        new(principal.GetRequiredUserId(), childId, search, page);
}

public sealed record MealieRecipeSummary(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string TotalTime,
    bool HasImage,
    // The family meal already linked to this recipe, if any (the picker marks it).
    MealId? LinkedMealId);

public sealed record MealieRecipePage(IReadOnlyList<MealieRecipeSummary> Items, int Page, int TotalPages, int Total);
