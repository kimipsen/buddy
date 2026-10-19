namespace buddy.Features.Mealplans;

// Marks a library meal as eaten away from home. A plan slot holding such a meal is "eating out":
// the AI assistant never offers the meal and never plans over the slot. See
// docs/backend/analysis/eating-out-meals.md.
public enum MealEatingOut
{
    None = 0,
    FriendsAndFamily = 1,
    Restaurant = 2
}
