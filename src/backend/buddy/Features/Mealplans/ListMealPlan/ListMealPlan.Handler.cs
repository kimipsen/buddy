using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListMealPlanHandler
{
    // Keeps a single request's join work bounded regardless of plan size -- same rationale and
    // value as ListTodaysDosesHandler.MaxRangeDays.
    public const int MaxRangeDays = 31;

    public static async Task<Result<IReadOnlyCollection<MealPlanEntry>>> Handle(
        ListMealPlan query,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation("'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > MaxRangeDays)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation($"The requested range cannot exceed {MaxRangeDays} days.");
        }

        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.NotFound();
        }

        var access = await MealplanAuthorization.CheckView(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<MealPlanEntry>>();
        }

        var entries = await MealPlanExpansion.ExpandAsync(query.ChildId, query.From, query.To, mealPlans, meals, guardians, cancellationToken);

        return new Result<IReadOnlyCollection<MealPlanEntry>>.Success(entries);
    }
}
