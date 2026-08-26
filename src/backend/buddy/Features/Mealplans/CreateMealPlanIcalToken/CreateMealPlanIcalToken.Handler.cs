using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class CreateMealPlanIcalTokenHandler
{
    public static async Task<Result<IssuedMealPlanIcalToken>> Handle(
        CreateMealPlanIcalToken command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<IssuedMealPlanIcalToken>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<IssuedMealPlanIcalToken>();
        }

        var (token, hash) = IcalToken.Generate();
        var tokenId = IcalTokenId.New();
        var now = DateTimeOffset.UtcNow;

        var existingMealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        MealPlanId planId;

        if (existingMealPlanId is null)
        {
            planId = MealPlanId.New();

            await mealPlans.CreateAsync(
                planId,
                [
                    new MealPlanCreated(planId, command.ChildId, now),
                    new MealPlanIcalTokenIssued(planId, tokenId, hash, userId, now)
                ],
                cancellationToken);
        }
        else
        {
            planId = existingMealPlanId;

            await mealPlans.AppendAsync(planId, [new MealPlanIcalTokenIssued(planId, tokenId, hash, userId, now)], cancellationToken);
        }

        return new Result<IssuedMealPlanIcalToken>.Success(new IssuedMealPlanIcalToken(tokenId, token, planId));
    }
}
