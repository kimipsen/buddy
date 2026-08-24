using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record AssignMealToSlotForGroup(UserId? UserId, GroupId GroupId, DateOnly Date, MealSlot Slot, MealId MealId, string? Notes)
{
    public static AssignMealToSlotForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, DateOnly date, MealSlot slot, MealId mealId, string? notes) =>
        new(principal.GetUserId(), groupId, date, slot, mealId, notes);
}
