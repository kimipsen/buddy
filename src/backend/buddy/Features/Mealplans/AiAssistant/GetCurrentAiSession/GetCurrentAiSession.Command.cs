using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record GetCurrentAiSession(UserId? UserId, UserId ChildId)
{
    public static GetCurrentAiSession FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetUserId(), childId);
}
