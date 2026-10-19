using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// The meals offered to the AI assistant: listed in the prompt and accepted by propose_assignment.
// FilterMatchedNothing is true when a filter is active and no meal passes it (must-include meals
// don't count), which StartAiSession and SendAiSessionMessage reject rather than call the provider.
// ReservedSlots are the session's slots that already hold an eating-out meal: the model is told
// they're taken, propose_assignment rejects them and ApplyAiSessionDraft skips them.
public sealed record AiMealSelection(
    IReadOnlyList<Meal> Meals,
    bool FilterMatchedNothing,
    IReadOnlyCollection<(DateOnly Date, MealSlot Slot)> ReservedSlots);

// Applies the per-session meal filter from AiSessionStarted -- see
// docs/backend/analysis/ai-assistant-meal-filter.md -- and the eating-out rules from
// docs/backend/analysis/eating-out-meals.md. Same "family-wide lookup as a static helper" shape as
// MealFamilyResolution.
public static class AiMealFilter
{
    public static bool IsActive(bool ratedOnly, AiServedWindow servedWithin) =>
        ratedOnly || servedWithin != AiServedWindow.Any;

    // The window is the N days before From ([From - N, From - 1]), so it doesn't move between a
    // session's turns and needs no time zone. Archived and eating-out meals are never offered;
    // must-include meals are offered whether or not they pass the filter (StartAiSession rejects
    // an eating-out must-include before it gets here).
    public static AiMealSelection Apply(
        IReadOnlyCollection<Meal> familyMeals,
        MealPlan? familyPlan,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots,
        bool ratedOnly,
        AiServedWindow servedWithin,
        IReadOnlyCollection<MealId> mustInclude)
    {
        var reservedSlots = FindReservedSlots(familyMeals, familyPlan, from, to, requestedSlots);
        var plannableMeals = familyMeals.Where(m => !m.IsArchived && m.EatingOut == MealEatingOut.None).ToList();

        if (!IsActive(ratedOnly, servedWithin))
        {
            return new AiMealSelection(plannableMeals, FilterMatchedNothing: false, reservedSlots);
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

        var matchingIds = plannableMeals
            .Where(m => !ratedOnly || m.Ratings.Count > 0)
            .Where(m => served is null || served.Contains(m.Id))
            .Select(m => m.Id)
            .ToHashSet();

        var offered = plannableMeals
            .Where(m => matchingIds.Contains(m.Id) || mustInclude.Contains(m.Id))
            .ToList();

        return new AiMealSelection(offered, FilterMatchedNothing: matchingIds.Count == 0, reservedSlots);
    }

    // Slots in [from, to] among the requested ones whose assigned meal is an eating-out meal --
    // archived ones included, since the family is still out that evening.
    public static IReadOnlyCollection<(DateOnly Date, MealSlot Slot)> FindReservedSlots(
        IReadOnlyCollection<Meal> familyMeals,
        MealPlan? familyPlan,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots)
    {
        if (familyPlan is null)
        {
            return [];
        }

        var eatingOutMealIds = familyMeals.Where(m => m.EatingOut != MealEatingOut.None).Select(m => m.Id).ToHashSet();

        return familyPlan.Assignments
            .Where(a => a.Key.Date >= from && a.Key.Date <= to && requestedSlots.Contains(a.Key.Slot))
            .Where(a => eatingOutMealIds.Contains(a.Value.MealId))
            .Select(a => a.Key)
            .Order()
            .ToList();
    }

    public static async Task<AiMealSelection> LoadAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots,
        bool ratedOnly,
        AiServedWindow servedWithin,
        IReadOnlyCollection<MealId> mustInclude,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        CancellationToken cancellationToken)
    {
        var familyMeals = await LoadFamilyMealsAsync(childId, guardians, meals, cancellationToken);
        var familyPlan = await LoadFamilyPlanAsync(childId, guardians, mealPlans, cancellationToken);

        return Apply(familyMeals, familyPlan, from, to, requestedSlots, ratedOnly, servedWithin, mustInclude);
    }

    // For ApplyAiSessionDraft, which re-checks the slots at apply time: one may have been marked
    // eating out after the model drafted it.
    public static async Task<IReadOnlyCollection<(DateOnly Date, MealSlot Slot)>> LoadReservedSlotsAsync(
        UserId childId,
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<MealSlot> requestedSlots,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        CancellationToken cancellationToken)
    {
        var familyMeals = await LoadFamilyMealsAsync(childId, guardians, meals, cancellationToken);
        var familyPlan = await LoadFamilyPlanAsync(childId, guardians, mealPlans, cancellationToken);

        return FindReservedSlots(familyMeals, familyPlan, from, to, requestedSlots);
    }

    private static async Task<List<Meal>> LoadFamilyMealsAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealEventStore meals, CancellationToken cancellationToken)
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

        return familyMeals;
    }

    // Always loaded: the reserved slots need it even when no served window is set. One snapshot
    // document per turn.
    private static async Task<MealPlan?> LoadFamilyPlanAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealPlanEventStore mealPlans, CancellationToken cancellationToken) =>
        await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken) is { } planId
            ? await mealPlans.FindSnapshotAsync(planId, cancellationToken)
            : null;
}
