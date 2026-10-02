using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record UpdateWorkLocation(UserId? UserId, WorkLocationId LocationId, string Name, string Icon, string Color)
{
    public static UpdateWorkLocation FromClaims(ClaimsPrincipal principal, WorkLocationId locationId, string name, string icon, string color) =>
        new(principal.GetUserId(), locationId, name, icon, color);
}
