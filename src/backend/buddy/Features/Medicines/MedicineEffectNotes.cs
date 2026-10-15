using buddy.Features.Users;

namespace buddy.Features.Medicines;

// What the three effect-note slices share: the child's schedules, the regimen of one day, and that
// day's live dose statuses (docs/backend/analysis/medicine-effect-notes.md, Question 4).
internal static class MedicineEffectNotes
{
    // Every schedule of the child, stopped ones included: a note written while a now-stopped
    // medicine was active still shows that medicine's dose statuses. Snapshots, not ReadAsync -- no
    // decision here appends to a schedule stream.
    public static async Task<IReadOnlyDictionary<MedicineId, MedicineSchedule>> LoadSchedulesAsync(
        UserId childId, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var schedules = new Dictionary<MedicineId, MedicineSchedule>();

        foreach (var medicineId in await medicines.ListIdsForChildAsync(childId, cancellationToken))
        {
            if (await medicines.FindSnapshotAsync(medicineId, cancellationToken) is { } schedule)
            {
                schedules[medicineId] = schedule;
            }
        }

        return schedules;
    }

    // The medicines the child was on that day, by the same rule as MedicineDoseExpansion: active
    // (not stopped) and the date inside the schedule's window. Empty on a baseline day.
    public static IReadOnlyList<RegimenMedicine> ResolveRegimen(IEnumerable<MedicineSchedule> schedules, DateOnly date) =>
        [.. schedules
            .Where(s => !s.IsStopped && IsInWindow(s, date))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Id.Value)
            .Select(s => new RegimenMedicine(s.Id, s.Name, s.Dosage))];

    // The schedule's current dose times on that date with their logged status (Pending when none).
    public static IReadOnlyList<RegimenDoseResponse> DosesOn(MedicineSchedule? schedule, DateOnly date) =>
        schedule is null || !IsInWindow(schedule, date)
            ? []
            : [.. schedule.Times
                .Order()
                .Select(time => new RegimenDoseResponse(time, schedule.DoseLog.GetValueOrDefault((date, time), DoseStatus.Pending)))];

    // "Today" on the child's own wall clock (User.TimeZoneId). Null for an unknown or deleted child.
    public static async Task<DateOnly?> ChildTodayAsync(UserId childId, IUserEventStore users, CancellationToken cancellationToken)
    {
        var child = await users.FindSnapshotAsync(childId, cancellationToken);

        if (child is null || child.IsDeleted)
        {
            return null;
        }

        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(child.TimeZoneId.Value));

        return DateOnly.FromDateTime(local.DateTime);
    }

    private static bool IsInWindow(MedicineSchedule schedule, DateOnly date) =>
        schedule.StartDate <= date && (schedule.EndDate is not { } end || date <= end);
}
