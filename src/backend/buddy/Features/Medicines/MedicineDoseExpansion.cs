using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Mirrors CalendarOccurrenceExpansion -- expands every non-stopped MedicineSchedule for a child
// into concrete doses within [from, to]. Nothing here is persisted or cached; it's recomputed from
// current aggregate state on every call. See
// docs/backend/analysis/medicine-schedules.md#dose-expansion--medicinedoseexpansion for why this
// deliberately resolves no time zone, unlike the calendar equivalent.
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
        var occurrences = new List<MedicineDoseOccurrence>();

        foreach (var medicineId in medicineIds)
        {
            var events = await medicines.ReadAsync(medicineId, cancellationToken);

            if (MedicineSchedule.Rehydrate(events) is not { IsStopped: false } schedule)
            {
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
