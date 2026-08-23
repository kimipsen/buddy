namespace buddy.Features.Mealplans;

// Resolves "what is child X's MealPlan stream ID" -- a MealPlan is a 1:1 singleton per child, but
// Marten streams are still addressed by their own aggregate ID, so this lookup is needed the same
// way MedicineIndexDocument is needed for "which schedules belong to child X". Written once on
// MealPlanCreated, never updated or removed afterwards.
public sealed record MealPlanIndexDocument(Guid Id, Guid ChildId);
