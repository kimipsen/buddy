using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Mirrors CalendarOccurrenceExpansion -- expands every non-stopped MedicineSchedule for a child
// into concrete doses within [from, to]. Nothing here is persisted or cached; it's recomputed from
// current aggregate state on every call. See
// docs/backend/analysis/medicine-schedules.md#dose-expansion--medicinedoseexpansion for why this
// deliberately resolves no time zone, unlike the calendar equivalent.
//
// includeStoppedHistory (the doctor visit summary) adds a stopped schedule's recorded doses --
// Taken or Skipped in its DoseLog -- but never expands it: MedicineScheduleStopped carries no stop
// date, so expanding would invent Pending doses up to `to`
// (docs/backend/analysis/doctor-visit-summary.md, Question 6).
public static class MedicineDoseExpansion
{
    public static async Task<IReadOnlyCollection<MedicineDoseOccurrence>> ExpandAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        IMedicineEventStore medicines,
        CancellationToken cancellationToken)
    {
        var medicineIds = await medicines.ListIdsForChildAsync(childId, cancellationToken);
        var schedules = new List<MedicineSchedule>(medicineIds.Count);

        foreach (var medicineId in medicineIds)
        {
            if (MedicineSchedule.Rehydrate(await medicines.ReadAsync(medicineId, cancellationToken)) is { } schedule)
            {
                schedules.Add(schedule);
            }
        }

        return Expand(schedules, from, to);
    }

    // For a caller that already holds the child's schedules (the doctor visit summary loads them
    // once for both the doses and the effect notes).
    public static IReadOnlyCollection<MedicineDoseOccurrence> Expand(
        IEnumerable<MedicineSchedule> schedules,
        DateOnly from,
        DateOnly to,
        bool includeStoppedHistory = false)
    {
        var occurrences = new List<MedicineDoseOccurrence>();

        foreach (var schedule in schedules)
        {
            if (schedule.IsStopped)
            {
                if (includeStoppedHistory)
                {
                    occurrences.AddRange(schedule.DoseLog
                        .Where(d => d.Key.Date >= from && d.Key.Date <= to && d.Value != DoseStatus.Pending)
                        .Select(d => new MedicineDoseOccurrence(
                            schedule.Id, schedule.Name, schedule.Dosage, schedule.Icon.Value, schedule.Color.Value,
                            d.Key.Date, d.Key.Time, d.Value)));
                }

                continue;
            }

            var rangeStart = schedule.StartDate > from ? schedule.StartDate : from;
            var rangeEnd = schedule.EndDate is { } end && end < to ? end : to;

            for (var date = rangeStart; date <= rangeEnd; date = date.AddDays(1))
            {
                foreach (var time in schedule.Times)
                {
                    var status = schedule.DoseLog.GetValueOrDefault((date, time), DoseStatus.Pending);

                    occurrences.Add(new MedicineDoseOccurrence(
                        schedule.Id, schedule.Name, schedule.Dosage, schedule.Icon.Value, schedule.Color.Value,
                        date, time, status));
                }
            }
        }

        occurrences.Sort((a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.Time.CompareTo(b.Time);
        });

        return occurrences;
    }
}
