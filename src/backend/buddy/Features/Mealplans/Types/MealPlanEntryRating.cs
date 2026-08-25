using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Flat, matching MealRatingResponse's shape (CreateMeal.Endpoint.cs) -- the same rating data, just
// surfaced through the plan-range read instead of the meal-library read.
public sealed record MealPlanEntryRating(UserId ChildId, int Stars, string? Comment, DateTimeOffset RatedAt);
