using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record CreateMedicineSchedule(
    UserId? UserId,
    UserId ChildId,
    string Name,
    string Dosage,
    Icon Icon,
    Color Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    public static CreateMedicineSchedule FromClaims(
        ClaimsPrincipal principal,
        UserId childId,
        string name,
        string dosage,
        Icon icon,
        Color color,
        IReadOnlyList<TimeOnly> times,
        DateOnly startDate,
        DateOnly? endDate) =>
        new(principal.GetUserId(), childId, name, dosage, icon, color, times, startDate, endDate);
}
