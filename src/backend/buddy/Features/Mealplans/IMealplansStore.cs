using Marten;

namespace buddy.Features.Mealplans;

// One schema shared by both Meal and MealPlan streams, the same way ICalendarsStore is shared by
// both Calendar and CalendarItem streams.
public interface IMealplansStore : IDocumentStore;
