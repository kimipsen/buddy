namespace buddy.Features.Mealplans;

// A Buddy meal's pointer to a Mealie recipe -- see docs/backend/analysis/mealie-integration.md,
// Question 4. RecipeId is stable; Slug and Name are as they were when the link was made, so a
// rename in Mealie keeps the link working (by id) and a deleted recipe still has a label.
public sealed record MealieRecipeRef(Guid RecipeId, string Slug, string Name);
