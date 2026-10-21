using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record DisconnectMealie(UserId UserId, UserId ChildId)
{
    public static DisconnectMealie FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
