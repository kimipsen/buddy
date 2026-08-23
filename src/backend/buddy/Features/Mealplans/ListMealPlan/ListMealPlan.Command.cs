using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealPlan(UserId? UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListMealPlan FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), childId, from, to);
}
