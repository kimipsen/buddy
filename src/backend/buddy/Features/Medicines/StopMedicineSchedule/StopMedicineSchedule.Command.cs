using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record StopMedicineSchedule(UserId? UserId, UserId ChildId, MedicineId MedicineId)
{
    public static StopMedicineSchedule FromClaims(ClaimsPrincipal principal, UserId childId, MedicineId medicineId) =>
        new(principal.GetUserId(), childId, medicineId);
}
