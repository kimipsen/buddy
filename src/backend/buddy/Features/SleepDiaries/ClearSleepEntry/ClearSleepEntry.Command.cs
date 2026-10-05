using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record ClearSleepEntry(UserId UserId, UserId ChildId, DateOnly Date)
{
    public static ClearSleepEntry FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly date) =>
        new(principal.GetRequiredUserId(), childId, date);
}
