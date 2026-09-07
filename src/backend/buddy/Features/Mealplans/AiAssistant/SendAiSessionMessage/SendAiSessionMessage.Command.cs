using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record SendAiSessionMessage(UserId? UserId, UserId ChildId, string Text)
{
    public static SendAiSessionMessage FromClaims(ClaimsPrincipal principal, UserId childId, string text) =>
        new(principal.GetUserId(), childId, text);
}
