using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record SetProviderApiKey(UserId? UserId, UserId ChildId, AiProvider Provider, string ApiKey)
{
    public static SetProviderApiKey FromClaims(ClaimsPrincipal principal, UserId childId, AiProvider provider, string apiKey) =>
        new(principal.GetUserId(), childId, provider, apiKey);
}
