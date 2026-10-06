using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class RevokeMealPlanIcalTokenHandler
{
    public static async Task<Result<Unit>> Handle(
        RevokeMealPlanIcalToken command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        ILogger<RevokeMealPlanIcalToken> logger,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents);

        if (plan is null || !plan.Tokens.ContainsKey(command.TokenId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await mealPlans.AppendAsync(
            mealPlanId,
            [new MealPlanIcalTokenRevoked(mealPlanId, command.TokenId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        logger.MealPlanIcalTokenRevoked(command.TokenId.Value, mealPlanId.Value, userId.Value);

        return new Result<Unit>.Success(Unit.Value);
    }
}
