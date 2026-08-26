using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record ListMealPlanIcalTokens(UserId? UserId, UserId ChildId)
{
    public static ListMealPlanIcalTokens FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}

// Never exposes the hash -- just enough for a guardian to recognize which token to revoke.
public sealed record MealPlanIcalTokenSummary(Guid TokenId, DateTimeOffset IssuedAt);
