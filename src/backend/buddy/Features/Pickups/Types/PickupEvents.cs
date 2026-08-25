using buddy.Features.Users;

namespace buddy.Features.Pickups;

public union PickupEvent(
    PickupScheduleCreated,
    PickupAssigned,
    PickupCleared
)
{
    public static PickupEvent FromPayload(object payload) => payload switch
    {
        PickupScheduleCreated e => e,
        PickupAssigned e => e,
        PickupCleared e => e,
        _ => throw new ArgumentException($"Unknown pickup event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        PickupScheduleCreated => nameof(PickupScheduleCreated),
        PickupAssigned => nameof(PickupAssigned),
        PickupCleared => nameof(PickupCleared),
    };
}

// Appended lazily by the first AssignPickup call for a child with no PickupSchedule stream yet,
// bundled into the same CreateAsync as that first PickupAssigned -- not provisioned as part of
// CreateChild, the same way MealPlanCreated/MedicineSchedule are decoupled from child creation.
public sealed record PickupScheduleCreated(PickupScheduleId Id, UserId ChildId, DateTimeOffset OccurredAt);

// Always overwrites (Before/After, no separate "reassign" event) -- no confirmation step
// server-side, the same rule MealAssignedToSlot uses.
public sealed record PickupAssigned(PickupScheduleId Id, DateOnly Date, PickupSlot Slot, PickupAssignment? Before, PickupAssignment After, DateTimeOffset OccurredAt);

public sealed record PickupCleared(PickupScheduleId Id, DateOnly Date, PickupSlot Slot, PickupAssignment Before, UserId ModifiedBy, DateTimeOffset OccurredAt);
