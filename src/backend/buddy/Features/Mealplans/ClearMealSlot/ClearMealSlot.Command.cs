using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ClearMealSlot(UserId? UserId, UserId ChildId, DateOnly Date, MealSlot Slot)
{
    public static ClearMealSlot FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date, MealSlot slot) =>
        new(principal.GetUserId(), childId, date, slot);
}
