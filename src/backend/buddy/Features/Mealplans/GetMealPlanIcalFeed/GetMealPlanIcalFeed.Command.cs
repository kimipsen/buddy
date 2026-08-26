namespace buddy.Features.Mealplans;

// Unauthenticated by design -- the token itself is the credential, mirroring Calendars'
// GetIcalFeed.Command.cs.
public sealed record GetMealPlanIcalFeed(MealPlanId MealPlanId, string Token);
