using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record ListFeelingCheckIns(UserId UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListFeelingCheckIns FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetRequiredUserId(), childId, from, to);
}
