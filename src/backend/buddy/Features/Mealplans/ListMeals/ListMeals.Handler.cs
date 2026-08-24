using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public static class ListMealsHandler
{
    public static async Task<Result<IReadOnlyCollection<Meal>>> Handle(
        ListMeals query,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<Meal>>.NotFound();
        }

        var access = await MealplanAuthorization.CheckView(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<Meal>>();
        }

        var loaded = await LoadFamilyMealsAsync(query.ChildId, meals, guardians, cancellationToken);

        return new Result<IReadOnlyCollection<Meal>>.Success(loaded);
    }

    // Shared with ListMealsForGroupHandler, which resolves its own AnchorChildId through a
    // group's MealplanPermissionPolicy instead of MealplanAuthorization -- everything past
    // authorization is identical (see docs/backend/analysis/group-owned-mealplans.md).
    internal static async Task<IReadOnlyCollection<Meal>> LoadFamilyMealsAsync(
        UserId childId, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var mealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);
        var loaded = new List<Meal>(mealIds.Count);

        foreach (var mealId in mealIds)
        {
            var events = await meals.ReadAsync(mealId, cancellationToken);

            // Deliberately includes archived meals -- a guardian's library of a child's dishes,
            // including retired ones, not just what's currently assignable.
            if (Meal.Rehydrate(events) is { } meal)
            {
                loaded.Add(meal);
            }
        }

        loaded.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        return loaded;
    }
}
