using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

public sealed record ListPickupSchedule(UserId? UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListPickupSchedule FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), childId, from, to);
}
