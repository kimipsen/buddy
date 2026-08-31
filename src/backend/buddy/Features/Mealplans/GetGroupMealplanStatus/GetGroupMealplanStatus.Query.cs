using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record GetGroupMealplanStatus(UserId? UserId, GroupId GroupId)
{
    public static GetGroupMealplanStatus FromClaims(ClaimsPrincipal principal, GroupId groupId) => new(principal.GetUserId(), groupId);
}
