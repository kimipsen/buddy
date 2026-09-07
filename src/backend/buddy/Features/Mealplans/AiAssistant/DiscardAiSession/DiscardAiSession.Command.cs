using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record DiscardAiSession(UserId? UserId, UserId ChildId)
{
    public static DiscardAiSession FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetUserId(), childId);
}
