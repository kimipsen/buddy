using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record RemoveProviderApiKey(UserId? UserId, UserId ChildId, AiProvider Provider)
{
    public static RemoveProviderApiKey FromClaims(ClaimsPrincipal principal, UserId childId, AiProvider provider) =>
        new(principal.GetUserId(), childId, provider);
}
