using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class ListMealPlanHandler
{
    // Keeps a single request's join work bounded regardless of plan size -- same rationale and
    // value as ListTodaysDosesHandler.MaxRangeDays.
    public const int MaxRangeDays = 31;

    public static async Task<Result<IReadOnlyCollection<MealPlanEntry>>> Handle(
        ListMealPlan query,
        IValidator<ListMealPlan> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation(problem);
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
