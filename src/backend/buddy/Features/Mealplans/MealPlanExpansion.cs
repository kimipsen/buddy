using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Joins a family's MealPlan assignments within [from, to] with each referenced Meal's current
// display data. Nothing here is persisted or cached; it's recomputed from current aggregate state
// on every call, the same contract MedicineDoseExpansion already has for Medicines.
public static class MealPlanExpansion
{
    public static async Task<IReadOnlyCollection<MealPlanEntry>> ExpandAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return [];
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);

        if (MealPlan.Rehydrate(planEvents) is not { } plan)
        {
            return [];
        }

        var mealsById = new Dictionary<MealId, Meal>();
        var entries = new List<MealPlanEntry>();

        foreach (var ((date, slot), assignment) in plan.Assignments)
        {
            if (date < from || date > to)
            {
                continue;
            }

            if (!mealsById.TryGetValue(assignment.MealId, out var meal))
            {
                var mealEvents = await meals.ReadAsync(assignment.MealId, cancellationToken);

                if (Meal.Rehydrate(mealEvents) is not { } loaded)
                {
                    // The referenced Meal stream is gone -- shouldn't happen in practice (meals
                    // are only archived, never deleted), so the entry is skipped rather than
                    // surfaced with missing data.
                    continue;
                }

                meal = loaded;
                mealsById[assignment.MealId] = meal;
            }

            // The viewing child's own rating, not every sibling's -- a personalized "do YOU like
            // tonight's dinner" for the calendar view (see MealResponse.Ratings for the full list).
            entries.Add(new MealPlanEntry(
                date, slot, meal.Id, meal.Name, meal.Icon.Value, meal.Color.Value, meal.Ratings.GetValueOrDefault(childId),
                assignment.Notes, assignment.AssignedBy.Value));
        }

        entries.Sort((a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.Slot.CompareTo(b.Slot);
        });

        return entries;
    }
}
