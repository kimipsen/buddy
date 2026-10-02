using System.Collections.Immutable;

using buddy.Common.Aggregates;
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
    public static PickupSchedule? Rehydrate(IEnumerable<PickupEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static PickupSchedule Replay(IEnumerable<PickupEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // PickupScheduleSnapshotProjection can drive the same logic one Marten-delivered event at a
    // time instead of duplicating this switch. Deliberately not named Apply/Create -- those names
    // are a convention JasperFx's projection source generator scans for on any type used as a
    // projection document, and PickupSchedule is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md).
    public static PickupSchedule Start(PickupEvent @event) => @event switch
    {
        PickupScheduleCreated created => new PickupSchedule(
            created.Id,
            created.ChildId,
            ImmutableDictionary<(DateOnly, PickupSlot), PickupAssignment>.Empty),
        _ => throw EventReplay.NotAStartEvent(nameof(PickupSchedule), @event.EventType)
    };

    public static PickupSchedule Advance(PickupSchedule schedule, PickupEvent @event) => @event switch
    {
        // Sparse dictionary: only slots a guardian actually filled hold a key, mirroring
        // MealPlan.Assignments/MedicineSchedule.DoseLog.
        PickupAssigned assigned => schedule with
        {
            Assignments = schedule.Assignments.SetItem((assigned.Date, assigned.Slot), assigned.After)
        },
        PickupCleared cleared => schedule with
        {
            Assignments = schedule.Assignments.Remove((cleared.Date, cleared.Slot))
        },
        PickupScheduleCreated => throw EventReplay.AlreadyStarted(nameof(PickupSchedule), @event.EventType)
    };
}
