using System.Security.Claims;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record RescheduleMedicineForGroup(UserId? UserId, GroupId GroupId, UserId ChildId, MedicineId MedicineId, IReadOnlyList<TimeOnly> Times, DateOnly StartDate, DateOnly? EndDate)
{
    public static RescheduleMedicineForGroup FromClaims(
        ClaimsPrincipal principal, GroupId groupId, UserId childId, MedicineId medicineId, IReadOnlyList<TimeOnly> times, DateOnly startDate, DateOnly? endDate) =>
        new(principal.GetUserId(), groupId, childId, medicineId, times, startDate, endDate);
}
