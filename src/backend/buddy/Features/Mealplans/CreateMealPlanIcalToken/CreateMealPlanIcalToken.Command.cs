using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record CreateMealPlanIcalToken(UserId? UserId, UserId ChildId)
{
    public static CreateMealPlanIcalToken FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}

// Token is the plaintext subscription secret -- returned exactly once, on creation, and never
// again. MealPlanId is needed here (rather than the ChildId already in the request) because the
// feed route is keyed by MealPlanId, not ChildId -- see docs/backend/analysis/mealplan-ical-feed.md.
public sealed record IssuedMealPlanIcalToken(IcalTokenId TokenId, string Token, MealPlanId MealPlanId);
