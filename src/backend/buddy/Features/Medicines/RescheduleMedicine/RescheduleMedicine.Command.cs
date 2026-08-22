using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record RescheduleMedicine(UserId? UserId, UserId ChildId, MedicineId MedicineId, IReadOnlyList<TimeOnly> Times, DateOnly StartDate, DateOnly? EndDate)
{
    public static RescheduleMedicine FromClaims(ClaimsPrincipal principal, UserId childId, MedicineId medicineId, IReadOnlyList<TimeOnly> times, DateOnly startDate, DateOnly? endDate) =>
        new(principal.GetUserId(), childId, medicineId, times, startDate, endDate);
}
