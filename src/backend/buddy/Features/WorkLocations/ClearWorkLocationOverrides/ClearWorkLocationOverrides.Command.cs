using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record ClearWorkLocationOverrides(UserId UserId, DateOnly From, DateOnly To)
{
    public static ClearWorkLocationOverrides FromClaims(ClaimsPrincipal principal, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), from, to);
}
