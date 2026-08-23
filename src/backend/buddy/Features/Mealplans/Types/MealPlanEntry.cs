namespace buddy.Features.Mealplans;

// A MealPlanAssignment joined with its Meal's current display data, for a child's plan view --
// never persisted, always assembled on read (see MealPlanExpansion), the same contract
// MedicineDoseOccurrence already has for Medicines.
public sealed record MealPlanEntry(
    DateOnly Date,
    MealSlot Slot,
    MealId MealId,
    string MealName,
    string Icon,
    string Color,
    MealRating? Rating,
    string? Notes,
    Guid AssignedBy);
