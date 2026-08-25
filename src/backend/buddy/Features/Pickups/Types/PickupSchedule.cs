using System.Collections.Immutable;

using buddy.Features.Users;

namespace buddy.Features.Pickups;

// One stream per child -- ChildId is stored directly, the same scope MedicineSchedule uses, not
// MealPlan's family-wide singleton (see docs/backend/analysis/pickup-schedules.md#question-2-
// per-child-or-family-wide-like-mealplan): different children's pickup/drop-off arrangements vary
// independently even within one family.
public sealed record PickupSchedule(
    PickupScheduleId Id,
    UserId ChildId,
    ImmutableDictionary<(DateOnly Date, PickupSlot Slot), PickupAssignment> Assignments)
{
    public static PickupSchedule? Rehydrate(IEnumerable<PickupEvent> events)
    {
        PickupSchedule? schedule = null;

        foreach (var @event in events)
        {
            schedule = @event switch
            {
                PickupScheduleCreated created => new PickupSchedule(
                    created.Id,
                    created.ChildId,
                    ImmutableDictionary<(DateOnly, PickupSlot), PickupAssignment>.Empty),
                // Sparse dictionary: only slots a guardian actually filled hold a key, mirroring
                // MealPlan.Assignments/MedicineSchedule.DoseLog.
                PickupAssigned assigned => schedule! with
                {
                    Assignments = schedule!.Assignments.SetItem((assigned.Date, assigned.Slot), assigned.After)
                },
                PickupCleared cleared => schedule! with
                {
                    Assignments = schedule!.Assignments.Remove((cleared.Date, cleared.Slot))
                },
                _ => schedule
            };
        }

        return schedule;
    }
}
