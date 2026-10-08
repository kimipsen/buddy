namespace buddy.Features.Mealplans;

// How far back a meal must have been served (assigned in the family MealPlan) to be offered to
// the AI assistant -- the N days before the session's From date (see
// docs/backend/analysis/ai-assistant-meal-filter.md, Question 3). The numeric value is the day
// count, so the frontend sends 0/30/60/90 the same way it sends MealSlot numerically.
public enum AiServedWindow
{
    Any = 0,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90
}
