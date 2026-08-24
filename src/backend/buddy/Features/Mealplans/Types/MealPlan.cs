using System.Collections.Immutable;

using buddy.Features.Groups;

namespace buddy.Features.Mealplans;

// No ChildId: a MealPlan is a family-wide singleton shared by every sibling (see
// MealFamilyResolution), not owned by the single child whose guardian happened to create it.
// SharedWithGroupId is additive, not an owner union like Calendar.Owner -- a MealPlan is always
// fundamentally family-owned; sharing with a group is an extra grant on top, never a replacement
// (see docs/backend/analysis/group-owned-mealplans.md).
public sealed record MealPlan(
    MealPlanId Id,
    ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment> Assignments,
    GroupId? SharedWithGroupId = null)
{
    public static MealPlan? Rehydrate(IEnumerable<MealPlanEvent> events)
    {
        MealPlan? plan = null;

        foreach (var @event in events)
        {
            plan = @event switch
            {
                MealPlanCreated created => new MealPlan(
                    created.Id,
                    ImmutableDictionary<(DateOnly, MealSlot), MealPlanAssignment>.Empty),
                // Sparse dictionary: only slots a guardian actually filled hold a key, so a plan
                // for a year is one small stream, not one entry per possible date/slot.
                MealAssignedToSlot assigned => plan! with
                {
                    Assignments = plan!.Assignments.SetItem((assigned.Date, assigned.Slot), assigned.After)
                },
                MealSlotCleared cleared => plan! with
                {
                    Assignments = plan!.Assignments.Remove((cleared.Date, cleared.Slot))
                },
                // At most one group at a time -- sharing with a second group simply overwrites
                // the first (see "Remaining open questions" in group-owned-mealplans.md).
                MealPlanSharedWithGroup shared => plan! with { SharedWithGroupId = shared.GroupId },
                MealPlanUnsharedFromGroup => plan! with { SharedWithGroupId = null },
                _ => plan
            };
        }

        return plan;
    }
}
