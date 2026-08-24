using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record UnshareMedicineFromGroup(UserId? UserId, UserId ChildId, GroupId GroupId)
{
    public static UnshareMedicineFromGroup FromClaims(ClaimsPrincipal principal, UserId childId, GroupId groupId) =>
        new(principal.GetUserId(), childId, groupId);
}
