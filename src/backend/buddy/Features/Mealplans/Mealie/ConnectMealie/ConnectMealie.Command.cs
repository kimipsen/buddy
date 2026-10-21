using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ConnectMealie(UserId UserId, UserId ChildId, string BaseUrl, string ApiToken)
{
    public static ConnectMealie FromClaims(ClaimsPrincipal principal, UserId childId, string baseUrl, string apiToken) =>
        new(principal.GetRequiredUserId(), childId, baseUrl, apiToken);
}
