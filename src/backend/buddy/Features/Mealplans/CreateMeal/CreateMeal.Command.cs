using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record CreateMeal(UserId? UserId, UserId ChildId, string Name, string? Description, Icon Icon, Color Color)
{
    public static CreateMeal FromClaims(ClaimsPrincipal principal, UserId childId, string name, string? description, Icon icon, Color color) =>
        new(principal.GetUserId(), childId, name, description, icon, color);
}
