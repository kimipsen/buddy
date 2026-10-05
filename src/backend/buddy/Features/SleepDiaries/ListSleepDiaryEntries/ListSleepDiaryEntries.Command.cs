using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record ListSleepDiaryEntries(UserId UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListSleepDiaryEntries FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), childId, from, to);
}
