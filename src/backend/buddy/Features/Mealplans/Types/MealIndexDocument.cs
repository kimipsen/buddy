namespace buddy.Features.Mealplans;

// Queryable read-model index kept alongside the Meal event stream, so a child's meals can be
// listed without scanning every stream in the store -- same problem MedicineIndexDocument solves
// for medicine schedules. Carries no "archived" flag: an archived Meal still belongs in ListMeals
// (a guardian's library, including retired dishes), so nothing is ever removed from this index.
public sealed record MealIndexDocument(Guid Id, Guid ChildId);
