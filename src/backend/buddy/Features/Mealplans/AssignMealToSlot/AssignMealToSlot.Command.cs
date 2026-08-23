using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record AssignMealToSlot(UserId? UserId, UserId ChildId, DateOnly Date, MealSlot Slot, MealId MealId, string? Notes)
{
    public static AssignMealToSlot FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date, MealSlot slot, MealId mealId, string? notes) =>
        new(principal.GetUserId(), childId, date, slot, mealId, notes);
}
