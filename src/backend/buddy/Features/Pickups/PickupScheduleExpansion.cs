using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Reads a child's PickupSchedule within [from, to]. Nothing here is persisted or cached; it's
// recomputed from current aggregate state on every call, the same contract
// MealPlanExpansion/MedicineDoseExpansion already have. No time zone resolution, same stance both
// precedents take -- Time (where present) is the child's own local wall-clock value.
public static class PickupScheduleExpansion
{
    public static async Task<IReadOnlyCollection<PickupOccurrence>> ExpandAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        IPickupScheduleEventStore pickups,
        CancellationToken cancellationToken)
    {
        var scheduleId = await pickups.FindIdForChildAsync(childId, cancellationToken);

        if (scheduleId is null)
        {
            return [];
        }

        var events = await pickups.ReadAsync(scheduleId, cancellationToken);

        if (PickupSchedule.Rehydrate(events) is not { } schedule)
        {
            return [];
        }

        var occurrences = new List<PickupOccurrence>();

        foreach (var ((date, slot), assignment) in schedule.Assignments)
        {
            if (date < from || date > to)
            {
                continue;
            }

            occurrences.Add(PickupOccurrence.FromAssignment(date, slot, assignment));
        }

        occurrences.Sort((a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.Slot.CompareTo(b.Slot);
        });

        return occurrences;
    }
}
