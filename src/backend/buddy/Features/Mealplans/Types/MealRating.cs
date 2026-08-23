namespace buddy.Features.Mealplans;

// The child's current opinion of a Meal -- not a history of every time they were asked (full
// history still exists via MealRated events). Stars is 1-5.
public sealed record MealRating(int Stars, string? Comment, DateTimeOffset RatedAt);
