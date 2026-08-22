using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record SetDoseStatus(UserId? UserId, UserId ChildId, MedicineId MedicineId, DateOnly Date, TimeOnly Time, DoseStatus Status)
{
    public static SetDoseStatus FromClaims(ClaimsPrincipal principal, UserId childId, MedicineId medicineId, DateOnly date, TimeOnly time, DoseStatus status) =>
        new(principal.GetUserId(), childId, medicineId, date, time, status);
}
