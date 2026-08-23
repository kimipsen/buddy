using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Meals and a MealPlan are shared by every child who has at least one active guardian in common
// with the requested child -- so two siblings never need two separate meal libraries or plans.
// There is no persisted "family"/"household" concept anywhere in this codebase (see
// docs/backend/analysis/mealplans.md), so sibling membership is recomputed from the existing
// GuardianLink graph on every call rather than stored -- the same "recomputed, not persisted"
// contract MealPlanExpansion already has for occurrences.
public static class MealFamilyResolution
{
    public static async Task<IReadOnlyCollection<UserId>> ResolveFamilyAsync(UserId childId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var family = new HashSet<UserId> { childId };
        var guardianLinks = await guardians.ListForChildAsync(childId, cancellationToken);

        foreach (var guardianLink in guardianLinks)
        {
            var siblingLinks = await guardians.ListForGuardianAsync(new UserId(guardianLink.GuardianId), cancellationToken);

            foreach (var siblingLink in siblingLinks)
            {
                family.Add(new UserId(siblingLink.ChildId));
            }
        }

        return family;
    }

    // Every Meal is indexed under whichever single child its guardian happened to be acting on
    // behalf of when it was created (see MartenMealEventStore.CreateAsync) -- sharing across
    // siblings happens entirely here, by widening the lookup to the whole family rather than by
    // writing extra index rows at creation time.
    public static async Task<IReadOnlyCollection<MealId>> ResolveFamilyMealIdsAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealEventStore meals, CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);
        var mealIds = new HashSet<MealId>();

        foreach (var member in family)
        {
            mealIds.UnionWith(await meals.ListIdsForChildAsync(member, cancellationToken));
        }

        return mealIds;
    }

    // A MealPlan is a family-wide singleton, so at most one sibling's index row should ever exist
    // in correct operation -- the first one found is returned.
    public static async Task<MealPlanId?> ResolveFamilyMealPlanIdAsync(
        UserId childId, IGuardianLinkEventStore guardians, IMealPlanEventStore mealPlans, CancellationToken cancellationToken)
    {
        var family = await ResolveFamilyAsync(childId, guardians, cancellationToken);

        foreach (var member in family)
        {
            if (await mealPlans.FindIdForChildAsync(member, cancellationToken) is { } id)
            {
                return id;
            }
        }

        return null;
    }
}
