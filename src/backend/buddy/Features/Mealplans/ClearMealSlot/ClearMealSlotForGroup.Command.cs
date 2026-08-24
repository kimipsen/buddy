using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ClearMealSlotForGroup(UserId? UserId, GroupId GroupId, DateOnly Date, MealSlot Slot)
{
    public static ClearMealSlotForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, DateOnly date, MealSlot slot) =>
        new(principal.GetUserId(), groupId, date, slot);
}
