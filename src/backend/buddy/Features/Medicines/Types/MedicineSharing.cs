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
    public static MedicineSharing? Rehydrate(IEnumerable<MedicineSharingEvent> events) => events.Aggregate((MedicineSharing?)null, Fold);

    // Single-event step, split out from Rehydrate so MedicineSharingSnapshotProjection can drive
    // the same logic one Marten-delivered event at a time instead of duplicating this switch.
    // Deliberately not named Apply/Create -- those names are a convention JasperFx's projection
    // source generator scans for on any type used as a projection document, and MedicineSharing is
    // that document (see Question 4/5 in docs/backend/analysis/event-stream-snapshots.md). The
    // sharing is null check below is preserved exactly as it was in Rehydrate -- MedicineSharedWithGroup
    // doubles as both the stream's creation event and a later re-share event (see
    // MedicineSharingEvents.cs), so this single case still has to distinguish "first event on this
    // stream" from "a later one" itself, same as before the split.
    public static MedicineSharing? Fold(MedicineSharing? sharing, MedicineSharingEvent @event) => @event switch
    {
        MedicineSharedWithGroup shared => sharing is null
            ? new MedicineSharing(shared.Id, shared.ChildId, shared.GroupId)
            : sharing with { SharedWithGroupId = shared.GroupId },
        MedicineUnsharedFromGroup => sharing! with { SharedWithGroupId = null },
        _ => sharing
    };
}
