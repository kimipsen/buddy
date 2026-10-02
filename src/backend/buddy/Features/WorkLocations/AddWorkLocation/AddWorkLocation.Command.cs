using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record AddWorkLocation(UserId? UserId, string Name, string Icon, string Color)
{
    public static AddWorkLocation FromClaims(ClaimsPrincipal principal, string name, string icon, string color) =>
        new(principal.GetUserId(), name, icon, color);
}
