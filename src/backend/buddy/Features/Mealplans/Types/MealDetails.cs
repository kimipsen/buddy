using buddy.Features.Calendars;

namespace buddy.Features.Mealplans;

// EatingOut defaults to None so MealDetailsUpdated events written before it existed still
// deserialize (System.Text.Json treats a defaulted constructor parameter as optional).
public sealed record MealDetails(string Name, string Description, Icon Icon, Color Color, MealEatingOut EatingOut = MealEatingOut.None);
