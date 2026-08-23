using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public union MealPlanEvent(
    MealPlanCreated,
    MealAssignedToSlot,
    MealSlotCleared
)
{
    public static MealPlanEvent FromPayload(object payload) => payload switch
    {
        MealPlanCreated e => e,
        MealAssignedToSlot e => e,
        MealSlotCleared e => e,
        _ => throw new ArgumentException($"Unknown meal plan event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        MealPlanCreated => nameof(MealPlanCreated),
        MealAssignedToSlot => nameof(MealAssignedToSlot),
        MealSlotCleared => nameof(MealSlotCleared),
    };
}

// Appended lazily by the first AssignMealToSlot call for a family with no MealPlan stream yet
// (see MealFamilyResolution), bundled into the same CreateAsync as that first MealAssignedToSlot
// -- not provisioned as part of CreateChild (Mealplans stays decoupled from Guardians the same way
// Medicines never hooks into child creation either). ChildId records which child the creating
// guardian was acting on behalf of, needed by MartenMealPlanEventStore.CreateAsync to seed the
// plan's first index row, but not projected onto the MealPlan aggregate itself -- the whole family
// shares this one stream, so "whose plan is this" isn't aggregate state. Two guardians assigning a
// family's very first slot at the same instant could race into two streams; accepted for v1 as the
// same class of last-write-wins tradeoff event sourcing already accepts elsewhere in this codebase
// (see docs/backend/analysis/mealplans.md).
public sealed record MealPlanCreated(MealPlanId Id, UserId ChildId, DateTimeOffset OccurredAt);

public sealed record MealAssignedToSlot(MealPlanId Id, DateOnly Date, MealSlot Slot, MealPlanAssignment? Before, MealPlanAssignment After, DateTimeOffset OccurredAt);

public sealed record MealSlotCleared(MealPlanId Id, DateOnly Date, MealSlot Slot, MealPlanAssignment Before, UserId ModifiedBy, DateTimeOffset OccurredAt);
