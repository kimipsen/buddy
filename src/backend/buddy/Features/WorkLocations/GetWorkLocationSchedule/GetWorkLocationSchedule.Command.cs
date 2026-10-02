using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record GetWorkLocationSchedule(UserId? UserId, UserId GuardianId)
{
    public static GetWorkLocationSchedule FromClaims(ClaimsPrincipal principal, UserId guardianId) =>
        new(principal.GetUserId(), guardianId);
}
