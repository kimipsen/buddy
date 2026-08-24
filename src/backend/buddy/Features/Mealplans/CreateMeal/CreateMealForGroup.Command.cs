using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record CreateMealForGroup(UserId? UserId, GroupId GroupId, string Name, string? Description, Icon Icon, Color Color)
{
    public static CreateMealForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, string name, string? description, Icon icon, Color color) =>
        new(principal.GetUserId(), groupId, name, description, icon, color);
}
