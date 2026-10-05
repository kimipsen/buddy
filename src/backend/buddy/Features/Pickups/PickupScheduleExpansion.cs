using buddy.Features.Babysitters;
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
        IBabysitterListEventStore babysitters,
        CancellationToken cancellationToken)
    {
        var scheduleId = await pickups.FindIdForChildAsync(childId, cancellationToken);

        if (scheduleId is null)
        {
            return [];
        }

        if (await pickups.FindSnapshotAsync(scheduleId, cancellationToken) is not { } schedule)
        {
            return [];
        }

        var inRange = schedule.Assignments.Where(entry => entry.Key.Date >= from && entry.Key.Date <= to).ToList();
        var babysitterNames = await BabysitterNames.LoadAsync(inRange.Select(entry => entry.Value.Assignee), babysitters, cancellationToken);

        var occurrences = inRange
            .Select(entry => PickupOccurrence.FromAssignment(entry.Key.Date, entry.Key.Slot, entry.Value, babysitterNames))
            .ToList();

        occurrences.Sort((a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.Slot.CompareTo(b.Slot);
        });

        return occurrences;
    }
}
