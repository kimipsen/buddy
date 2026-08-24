using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record UnshareMealPlanFromGroup(UserId? UserId, UserId ChildId, GroupId GroupId)
{
    public static UnshareMealPlanFromGroup FromClaims(ClaimsPrincipal principal, UserId childId, GroupId groupId) =>
        new(principal.GetUserId(), childId, groupId);
}
