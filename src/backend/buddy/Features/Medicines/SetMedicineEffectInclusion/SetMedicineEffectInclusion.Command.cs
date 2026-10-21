using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record SetMedicineEffectInclusion(UserId UserId, UserId ChildId, MedicineId MedicineId, bool Included)
{
    public static SetMedicineEffectInclusion FromClaims(ClaimsPrincipal principal, UserId childId, MedicineId medicineId, bool included) =>
        new(principal.GetRequiredUserId(), childId, medicineId, included);
}
