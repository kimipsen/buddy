using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record TestMealieConnection(UserId UserId, UserId ChildId)
{
    public static TestMealieConnection FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
