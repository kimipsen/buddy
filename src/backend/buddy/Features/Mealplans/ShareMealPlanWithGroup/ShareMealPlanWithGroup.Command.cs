using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ShareMealPlanWithGroup(UserId? UserId, UserId ChildId, GroupId GroupId)
{
    public static ShareMealPlanWithGroup FromClaims(ClaimsPrincipal principal, UserId childId, GroupId groupId) =>
        new(principal.GetUserId(), childId, groupId);
}
