using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record ListSleepDiaryShareLinks(UserId UserId, UserId ChildId)
{
    public static ListSleepDiaryShareLinks FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
