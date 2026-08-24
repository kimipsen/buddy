using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public sealed record SharedMealplanGroup(GroupId Id, string Name);

// The only read path for "is this family's plan currently shared, and with which group" -- gated
// on Manage tier (guardian only), the same principal who can share/unshare in the first place.
public static class GetSharedGroupHandler
{
    public static async Task<Result<SharedMealplanGroup?>> Handle(
        GetSharedGroup query, IMealPlanEventStore mealPlans, IGuardianLinkEventStore guardians, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<SharedMealplanGroup?>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<SharedMealplanGroup?>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(query.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<SharedMealplanGroup?>.Success(null);
        }

        var plan = MealPlan.Rehydrate(await mealPlans.ReadAsync(mealPlanId, cancellationToken))!;

        if (plan.SharedWithGroupId is not { } groupId)
        {
            return new Result<SharedMealplanGroup?>.Success(null);
        }

        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        return new Result<SharedMealplanGroup?>.Success(group is null ? null : new SharedMealplanGroup(groupId, group.Name));
    }
}
