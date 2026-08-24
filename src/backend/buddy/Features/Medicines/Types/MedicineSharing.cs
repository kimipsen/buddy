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
    public static MedicineSharing? Rehydrate(IEnumerable<MedicineSharingEvent> events)
    {
        MedicineSharing? sharing = null;

        foreach (var @event in events)
        {
            sharing = @event switch
            {
                MedicineSharedWithGroup shared => sharing is null
                    ? new MedicineSharing(shared.Id, shared.ChildId, shared.GroupId)
                    : sharing with { SharedWithGroupId = shared.GroupId },
                MedicineUnsharedFromGroup => sharing! with { SharedWithGroupId = null },
                _ => sharing
            };
        }

        return sharing;
    }
}
