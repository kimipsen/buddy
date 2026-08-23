using buddy.Common;
using buddy.Features.Guardians;

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

        var mealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(query.ChildId, guardians, meals, cancellationToken);
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

        return new Result<IReadOnlyCollection<Meal>>.Success(loaded);
    }
}
