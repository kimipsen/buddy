using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record GetSharedMedicineGroup(UserId? UserId, UserId ChildId)
{
    public static GetSharedMedicineGroup FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
