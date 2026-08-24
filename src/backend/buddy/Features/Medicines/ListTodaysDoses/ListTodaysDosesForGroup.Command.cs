using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ListTodaysDosesForGroup(UserId? UserId, GroupId GroupId, UserId ChildId, DateOnly From, DateOnly To)
{
    public static ListTodaysDosesForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId childId, DateOnly from, DateOnly to) =>
        new(principal.GetUserId(), groupId, childId, from, to);
}
