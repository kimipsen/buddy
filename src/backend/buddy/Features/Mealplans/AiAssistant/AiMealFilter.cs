using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// The meals offered to the AI assistant: listed in the prompt and accepted by propose_assignment.
// FilterMatchedNothing is true when a filter is active and no meal passes it (must-include meals
// don't count), which StartAiSession and SendAiSessionMessage reject rather than call the provider.
public sealed record AiMealSelection(IReadOnlyList<Meal> Meals, bool FilterMatchedNothing);

// Applies the per-session meal filter from AiSessionStarted -- see
// docs/backend/analysis/ai-assistant-meal-filter.md. Same "family-wide lookup as a static helper"
// shape as MealFamilyResolution.
public static class AiMealFilter
{
    public static bool IsActive(bool ratedOnly, AiServedWindow servedWithin) =>
        ratedOnly || servedWithin != AiServedWindow.Any;

    // The window is the N days before From ([From - N, From - 1]), so it doesn't move between a
    // session's turns and needs no time zone. Archived meals are never offered; must-include
    // meals are offered whether or not they pass the filter.
    public static AiMealSelection Apply(
        IReadOnlyCollection<Meal> familyMeals,
        MealPlan? familyPlan,
        DateOnly from,
        bool ratedOnly,
        AiServedWindow servedWithin,
        IReadOnlyCollection<MealId> mustInclude)
    {
        var activeMeals = familyMeals.Where(m => !m.IsArchived).ToList();

        if (!IsActive(ratedOnly, servedWithin))
        {
            return new AiMealSelection(activeMeals, FilterMatchedNothing: false);
        }

        HashSet<MealId>? served = null;

        if (servedWithin != AiServedWindow.Any)
        {
            var windowStart = from.AddDays(-(int)servedWithin);
            served = familyPlan is null
                ? []
                : [.. familyPlan.Assignments
                    .Where(a => a.Key.Date >= windowStart && a.Key.Date < from)
                    .Select(a => a.Value.MealId)];
        }

        var matchingIds = activeMeals
            .Where(m => !ratedOnly || m.Ratings.Count > 0)
            .Where(m => served is null || served.Contains(m.Id))
            .Select(m => m.Id)
            .ToHashSet();

        var offered = activeMeals
            .Where(m => matchingIds.Contains(m.Id) || mustInclude.Contains(m.Id))
            .ToList();

        return new AiMealSelection(offered, FilterMatchedNothing: matchingIds.Count == 0);
    }

    public static async Task<AiMealSelection> LoadAsync(
        UserId childId,
        DateOnly from,
        bool ratedOnly,
        AiServedWindow servedWithin,
        IReadOnlyCollection<MealId> mustInclude,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        CancellationToken cancellationToken)
    {
        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);
        List<Meal> familyMeals = [];

        // Snapshots, not ReadAsync + Rehydrate: the meals are only read, never appended to here, so
        // a full stream replay per meal would buy nothing (and a family's library can be long).
        foreach (var mealId in familyMealIds)
        {
            if (await meals.FindSnapshotAsync(mealId, cancellationToken) is { } meal)
            {
                familyMeals.Add(meal);
            }
        }

        MealPlan? familyPlan = null;

        // The plan is only needed for the served window, so an unfiltered session skips the read.
        if (servedWithin != AiServedWindow.Any
            && await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken) is { } planId)
        {
            familyPlan = await mealPlans.FindSnapshotAsync(planId, cancellationToken);
        }

        return Apply(familyMeals, familyPlan, from, ratedOnly, servedWithin, mustInclude);
    }
}
