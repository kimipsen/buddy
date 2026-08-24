using System.Security.Claims;

using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public sealed record CreateMedicineScheduleForGroup(
    UserId? UserId,
    GroupId GroupId,
    UserId ChildId,
    string Name,
    string Dosage,
    Icon Icon,
    Color Color,
    IReadOnlyList<TimeOnly> Times,
    DateOnly StartDate,
    DateOnly? EndDate)
{
    public static CreateMedicineScheduleForGroup FromClaims(
        ClaimsPrincipal principal,
        GroupId groupId,
        UserId childId,
        string name,
        string dosage,
        Icon icon,
        Color color,
        IReadOnlyList<TimeOnly> times,
        DateOnly startDate,
        DateOnly? endDate) =>
        new(principal.GetUserId(), groupId, childId, name, dosage, icon, color, times, startDate, endDate);
}
