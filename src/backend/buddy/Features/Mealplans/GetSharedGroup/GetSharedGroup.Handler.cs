using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// The only read path for "is this family's plan currently shared, and with which group" -- gated
// on Manage tier (guardian only), the same principal who can share/unshare in the first place.
public static class GetSharedGroupHandler
{
    public static async Task<Result<GroupId?>> Handle(
        GetSharedGroup query, IMealPlanEventStore mealPlans, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<GroupId?>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<GroupId?>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(query.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<GroupId?>.Success(null);
        }

        var plan = MealPlan.Rehydrate(await mealPlans.ReadAsync(mealPlanId, cancellationToken))!;

        return new Result<GroupId?>.Success(plan.SharedWithGroupId);
    }
}
