using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealieShoppingLists(UserId UserId, UserId ChildId)
{
    public static ListMealieShoppingLists FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}

public sealed record MealieShoppingList(Guid Id, string Name);
