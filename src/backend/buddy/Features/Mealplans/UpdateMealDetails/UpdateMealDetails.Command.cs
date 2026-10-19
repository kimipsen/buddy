using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record UpdateMealDetails(UserId UserId, UserId ChildId, MealId MealId, string Name, string Description, Icon Icon, Color Color, MealEatingOut EatingOut)
{
    public static UpdateMealDetails FromClaims(ClaimsPrincipal principal, UserId childId, MealId mealId, string name, string description, Icon icon, Color color, MealEatingOut eatingOut) =>
        new(principal.GetRequiredUserId(), childId, mealId, name, description, icon, color, eatingOut);
}
