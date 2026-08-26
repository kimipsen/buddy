using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListMealPlanIcalTokensHandler
{
    public static async Task<Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>> Handle(
        ListMealPlanIcalTokens query,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<MealPlanIcalTokenSummary>>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(query.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Success([]);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);

        if (MealPlan.Rehydrate(planEvents) is not { } plan)
        {
            return new Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Success([]);
        }

        var tokens = plan.Tokens
            .Select(kv => new MealPlanIcalTokenSummary(kv.Key.Value, kv.Value.IssuedAt))
            .ToArray();

        return new Result<IReadOnlyCollection<MealPlanIcalTokenSummary>>.Success(tokens);
    }
}
