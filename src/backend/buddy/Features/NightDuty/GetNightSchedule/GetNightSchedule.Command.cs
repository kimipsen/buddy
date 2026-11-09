using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public sealed record GetNightSchedule(UserId UserId, UserId ChildId)
{
    public static GetNightSchedule FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
