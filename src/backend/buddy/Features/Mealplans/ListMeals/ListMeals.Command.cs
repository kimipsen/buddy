using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMeals(UserId? UserId, UserId ChildId)
{
    public static ListMeals FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
