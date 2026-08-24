using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealsForGroup(UserId? UserId, GroupId GroupId)
{
    public static ListMealsForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId) => new(principal.GetUserId(), groupId);
}
