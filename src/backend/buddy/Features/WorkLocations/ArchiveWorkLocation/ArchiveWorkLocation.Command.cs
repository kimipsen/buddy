using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record ArchiveWorkLocation(UserId? UserId, WorkLocationId LocationId)
{
    public static ArchiveWorkLocation FromClaims(ClaimsPrincipal principal, WorkLocationId locationId) =>
        new(principal.GetUserId(), locationId);
}
