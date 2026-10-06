using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealPlanImports(UserId UserId, UserId ChildId)
{
    public static ListMealPlanImports FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}

public sealed record ListMealPlanImportsForGroup(UserId UserId, GroupId GroupId)
{
    public static ListMealPlanImportsForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId) => new(principal.GetRequiredUserId(), groupId);
}
