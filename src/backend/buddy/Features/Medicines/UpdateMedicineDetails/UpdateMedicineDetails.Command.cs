using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record UpdateMedicineDetails(UserId? UserId, UserId ChildId, MedicineId MedicineId, string Name, string Dosage, Icon Icon, Color Color)
{
    public static UpdateMedicineDetails FromClaims(ClaimsPrincipal principal, UserId childId, MedicineId medicineId, string name, string dosage, Icon icon, Color color) =>
        new(principal.GetUserId(), childId, medicineId, name, dosage, icon, color);
}
