using buddy.Common.Aggregates;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// A 1:1 singleton per child recording whether their medicine schedules are currently shared with
// a group -- lazily created the same way MealPlan is (see docs/backend/analysis/mealplans.md),
// but carries no domain content of its own beyond that single flag: unlike Meal/MealPlan, medicine
// schedules stay child-scoped (MedicineSchedule.ChildId) rather than family-wide, so sharing is a
// separate per-child on/off switch instead of a field on the schedule itself (see
// docs/backend/analysis/medicine-schedules.md).
public sealed record MedicineSharing(MedicineSharingId Id, UserId ChildId, GroupId? SharedWithGroupId)
{
    public static MedicineSharing? Rehydrate(IEnumerable<MedicineSharingEvent> events) => EventReplay.Rehydrate(events, Start, Advance);

    public static MedicineSharing Replay(IEnumerable<MedicineSharingEvent> events) => EventReplay.Replay(events, Start, Advance);

    // Single-event steps (Start for the creation event, Advance for every later one; not Evolve
    // either, another JasperFx convention), split out from Rehydrate so
    // MedicineSharingSnapshotProjection can drive the same logic one Marten-delivered event at a
    // time. Deliberately not named Apply/Create -- those names are a convention JasperFx's
    // projection source generator scans for on any type used as a projection document, and
    // MedicineSharing is that document (see Question 4/5 in
    // docs/backend/analysis/event-stream-snapshots.md). MedicineSharedWithGroup both creates the
    // stream and re-shares it later (see MedicineSharingEvents.cs): as the first event it is Start,
    // afterwards it is an Advance step -- no "is this the first event" null check needed.
    public static MedicineSharing Start(MedicineSharingEvent @event) => @event switch
    {
        MedicineSharedWithGroup shared => new MedicineSharing(shared.Id, shared.ChildId, shared.GroupId),
        _ => throw EventReplay.NotAStartEvent(nameof(MedicineSharing), @event.EventType)
    };

    public static MedicineSharing Advance(MedicineSharing sharing, MedicineSharingEvent @event) => @event switch
    {
        MedicineSharedWithGroup shared => sharing with { SharedWithGroupId = shared.GroupId },
        MedicineUnsharedFromGroup => sharing with { SharedWithGroupId = null },
    };
}
