using System.Collections.Immutable;

namespace buddy.Features.Mealplans;

// No ChildId: a MealPlan is a family-wide singleton shared by every sibling (see
// MealFamilyResolution), not owned by the single child whose guardian happened to create it.
public sealed record MealPlan(
    MealPlanId Id,
    ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment> Assignments)
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
                _ => plan
            };
        }

        return plan;
    }
}
