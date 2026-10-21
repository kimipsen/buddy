using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record AddMealPlanToMealieShoppingList(UserId UserId, UserId ChildId, Guid ListId, DateOnly From, DateOnly To, IReadOnlyCollection<MealSlot> Slots)
{
    public static AddMealPlanToMealieShoppingList FromClaims(
        ClaimsPrincipal principal, UserId childId, Guid listId, DateOnly from, DateOnly to, IReadOnlyCollection<MealSlot> slots) =>
        new(principal.GetRequiredUserId(), childId, listId, from, to, slots);
}

public sealed record AddedRecipe(Guid RecipeId, string RecipeName, int Times);

// What was sent to Mealie, and what wasn't and why: meals without a linked recipe (by name, once
// each) and eating-out slots, which never go on a shopping list.
public sealed record AddedToShoppingList(IReadOnlyList<AddedRecipe> Added, IReadOnlyList<string> SkippedUnlinked, int SkippedEatingOut);
