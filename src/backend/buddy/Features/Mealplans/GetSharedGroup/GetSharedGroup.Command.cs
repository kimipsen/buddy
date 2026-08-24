using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record GetSharedGroup(UserId? UserId, UserId ChildId)
{
    public static GetSharedGroup FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
