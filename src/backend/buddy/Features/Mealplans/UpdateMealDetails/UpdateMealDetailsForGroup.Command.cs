using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record UpdateMealDetailsForGroup(UserId? UserId, GroupId GroupId, MealId MealId, string Name, string? Description, Icon Icon, Color Color)
{
    public static UpdateMealDetailsForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, MealId mealId, string name, string? description, Icon icon, Color color) =>
        new(principal.GetUserId(), groupId, mealId, name, description, icon, color);
}
