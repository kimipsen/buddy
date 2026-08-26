using System.Collections.Immutable;
using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record UpdateMealSlotTimes(UserId? UserId, UserId ChildId, ImmutableDictionary<MealSlot, TimeOnly> Times)
{
    public static UpdateMealSlotTimes FromClaims(ClaimsPrincipal principal, UserId childId, ImmutableDictionary<MealSlot, TimeOnly> times) =>
        new(principal.GetUserId(), childId, times);
}
