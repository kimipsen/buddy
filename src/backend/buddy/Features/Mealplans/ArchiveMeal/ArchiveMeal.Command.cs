using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ArchiveMeal(UserId? UserId, UserId ChildId, MealId MealId)
{
    public static ArchiveMeal FromClaims(ClaimsPrincipal principal, UserId childId, MealId mealId) =>
        new(principal.GetUserId(), childId, mealId);
}
