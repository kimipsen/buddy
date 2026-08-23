using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record RateMeal(UserId? UserId, UserId ChildId, MealId MealId, int Stars, string? Comment)
{
    public static RateMeal FromClaims(ClaimsPrincipal principal, UserId childId, MealId mealId, int stars, string? comment) =>
        new(principal.GetUserId(), childId, mealId, stars, comment);
}
