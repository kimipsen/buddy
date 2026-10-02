using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record ListWorkDays(UserId? UserId, UserId GuardianId, DateOnly From, DateOnly To)
{
    public static ListWorkDays FromClaims(ClaimsPrincipal principal, UserId guardianId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), guardianId, from, to);
}
