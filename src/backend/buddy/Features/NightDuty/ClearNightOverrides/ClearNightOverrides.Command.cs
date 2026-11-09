using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public sealed record ClearNightOverrides(UserId UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ClearNightOverrides FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), childId, from, to);
}
