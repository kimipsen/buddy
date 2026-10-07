using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record AcknowledgeAiDataSharing(UserId UserId, UserId ChildId)
{
    public static AcknowledgeAiDataSharing FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
