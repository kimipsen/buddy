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
    public static PickupSchedule? Rehydrate(IEnumerable<PickupEvent> events) => events.Aggregate((PickupSchedule?)null, Fold);

    // Single-event step, split out from Rehydrate so PickupScheduleSnapshotProjection can drive
    // the same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and PickupSchedule is
    // that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md).
    public static PickupSchedule? Fold(PickupSchedule? schedule, PickupEvent @event) => @event switch
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
