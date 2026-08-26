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

        return await ExpandFromPlanAsync(plan, from, to, meals, childId, cancellationToken);
    }

    // Split out of ExpandAsync so a caller that already has a rehydrated MealPlan in hand -- e.g.
    // GetMealPlanIcalFeedHandler, which rehydrates the plan to check the feed token before it can
    // even know which entries to expand -- doesn't need a second resolve-by-childId round trip.
    // viewerId is null for a caller with no single "viewing child" (the anonymous iCal feed serves
    // the whole family at once), in which case Rating is left null on every entry -- AllRatings
    // still carries every sibling's rating regardless.
    public static async Task<IReadOnlyCollection<MealPlanEntry>> ExpandFromPlanAsync(
        MealPlan plan,
        DateOnly from,
        DateOnly to,
        IMealEventStore meals,
        UserId? viewerId,
        CancellationToken cancellationToken)
    {
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

            // Rating is the viewing child's own opinion, for a personalized "do YOU like tonight's
            // dinner" in the calendar view; AllRatings carries every sibling's, for a guardian
            // comparing reactions across a shared meal (see MealResponse.Ratings for the same data
            // in the meal-library view).
            entries.Add(new MealPlanEntry(
                date, slot, meal.Id, meal.Name, meal.Icon.Value, meal.Color.Value,
                viewerId is { } viewer ? meal.Ratings.GetValueOrDefault(viewer) : null,
                assignment.Notes, assignment.AssignedBy.Value,
                [.. meal.Ratings.Select(pair => new MealPlanEntryRating(pair.Key, pair.Value.Stars, pair.Value.Comment, pair.Value.RatedAt))]));
        }

        entries.Sort((a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : a.Slot.CompareTo(b.Slot);
        });

        return entries;
    }
}
