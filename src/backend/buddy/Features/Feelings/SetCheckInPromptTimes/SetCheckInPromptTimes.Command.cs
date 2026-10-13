using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record SetCheckInPromptTimes(UserId UserId, UserId ChildId, IReadOnlyList<TimeOnly> Times)
{
    public static SetCheckInPromptTimes FromClaims(ClaimsPrincipal principal, UserId childId, IReadOnlyList<TimeOnly> times) =>
        new(principal.GetRequiredUserId(), childId, times);
}
