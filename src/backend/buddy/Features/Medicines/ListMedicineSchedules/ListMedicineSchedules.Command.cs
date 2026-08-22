using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record ListMedicineSchedules(UserId? UserId, UserId ChildId)
{
    public static ListMedicineSchedules FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
