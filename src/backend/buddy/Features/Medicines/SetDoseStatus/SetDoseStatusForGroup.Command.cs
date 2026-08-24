using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record SetDoseStatusForGroup(UserId? UserId, GroupId GroupId, UserId ChildId, MedicineId MedicineId, DateOnly Date, TimeOnly Time, DoseStatus Status)
{
    public static SetDoseStatusForGroup FromClaims(ClaimsPrincipal principal, GroupId groupId, UserId childId, MedicineId medicineId, DateOnly date, TimeOnly time, DoseStatus status) =>
        new(principal.GetUserId(), groupId, childId, medicineId, date, time, status);
}
