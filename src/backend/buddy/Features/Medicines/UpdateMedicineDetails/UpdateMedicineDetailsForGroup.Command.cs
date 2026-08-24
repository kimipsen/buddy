using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record UpdateMedicineDetailsForGroup(UserId? UserId, GroupId GroupId, UserId ChildId, MedicineId MedicineId, string Name, string Dosage, Icon Icon, Color Color)
{
    public static UpdateMedicineDetailsForGroup FromClaims(
        ClaimsPrincipal principal, GroupId groupId, UserId childId, MedicineId medicineId, string name, string dosage, Icon icon, Color color) =>
        new(principal.GetUserId(), groupId, childId, medicineId, name, dosage, icon, color);
}
