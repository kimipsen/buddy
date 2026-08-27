using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

public sealed record CreateTaskTemplate(UserId? UserId, UserId ChildId, string Name, Icon Icon, Color Color)
{
    public static CreateTaskTemplate FromClaims(ClaimsPrincipal principal, UserId childId, string name, Icon icon, Color color) =>
        new(principal.GetUserId(), childId, name, icon, color);
}
