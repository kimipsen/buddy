using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ListMedicineSchedulesForGroup(UserId? UserId, GroupId GroupId, UserId ChildId)
{
    public static ListMedicineSchedulesForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId childId) =>
        new(principal.GetUserId(), groupId, childId);
}
