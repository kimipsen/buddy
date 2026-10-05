using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record CreateSleepDiaryShareLink(UserId UserId, UserId ChildId, DateTimeOffset? ExpiresAt)
{
    public static CreateSleepDiaryShareLink FromClaims(ClaimsPrincipal principal, UserId childId, DateTimeOffset? expiresAt) =>
        new(principal.GetRequiredUserId(), childId, expiresAt);
}
