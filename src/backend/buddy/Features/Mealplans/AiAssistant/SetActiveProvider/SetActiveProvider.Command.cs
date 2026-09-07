using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record SetActiveProvider(UserId? UserId, UserId ChildId, AiProvider Provider)
{
    public static SetActiveProvider FromClaims(ClaimsPrincipal principal, UserId childId, AiProvider provider) =>
        new(principal.GetUserId(), childId, provider);
}
