using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

// LocationId null = "not at any location" for every date in [From, To] (a holiday, a day off).
public sealed record SetWorkLocationOverrides(UserId? UserId, DateOnly From, DateOnly To, WorkLocationId? LocationId)
{
    public static SetWorkLocationOverrides FromClaims(ClaimsPrincipal principal, DateOnly from, DateOnly to, WorkLocationId? locationId) =>
        new(principal.GetUserId(), from, to, locationId);
}
