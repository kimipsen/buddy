using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ListTodaysDoses(UserId? UserId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListTodaysDoses FromClaims(ClaimsPrincipal principal, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), childId, from, to);
}
