using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ApplyAiSessionDraft(UserId? UserId, UserId ChildId)
{
    public static ApplyAiSessionDraft FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetUserId(), childId);
}
