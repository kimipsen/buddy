using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Whether the plan is shared with a group right now. A group that no longer exists counts as not
// shared.
public union MealplanGroupShare(MealplanGroupShare.NotShared, MealplanGroupShare.Shared)
{
    public sealed record NotShared;

    public sealed record Shared(GroupId Id, string Name);
}

// The only read path for "is this family's plan currently shared, and with which group" -- gated
// on Manage tier (guardian only), the same principal who can share/unshare in the first place.
public static class GetSharedGroupHandler
{
    public static async Task<Result<MealplanGroupShare>> Handle(
        GetSharedGroup query, IMealPlanEventStore mealPlans, IGuardianLinkEventStore guardians, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var access = await MealplanAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealplanGroupShare>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(query.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<MealplanGroupShare>.Success(new MealplanGroupShare.NotShared());
        }

        var plan = await mealPlans.FindSnapshotAsync(mealPlanId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot for mealPlanId {mealPlanId}, although its index says the stream exists.");

        if (plan.SharedWithGroupId is not { } groupId)
        {
            return new Result<MealplanGroupShare>.Success(new MealplanGroupShare.NotShared());
        }

        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        return new Result<MealplanGroupShare>.Success(group is null
            ? new MealplanGroupShare.NotShared()
            : new MealplanGroupShare.Shared(groupId, group.Name));
    }
}
