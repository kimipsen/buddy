using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public sealed record MealPlanAssignment(MealId MealId, UserId AssignedBy, string? Notes);
