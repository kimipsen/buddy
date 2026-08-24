using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record StopMedicineScheduleForGroup(UserId? UserId, GroupId GroupId, UserId ChildId, MedicineId MedicineId)
{
    public static StopMedicineScheduleForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId childId, MedicineId medicineId) =>
        new(principal.GetUserId(), groupId, childId, medicineId);
}
