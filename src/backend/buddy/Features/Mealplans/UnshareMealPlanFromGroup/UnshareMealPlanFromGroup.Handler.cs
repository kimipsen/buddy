using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

// Deliberately asymmetric with ShareMealPlanWithGroupHandler: granting access needs both the
// family's and the group's consent, but revoking only needs the family's -- a guardian should
// always be able to cut off a share they no longer want, regardless of their standing (or lack
// of one) in the group on the other end.
public static class UnshareMealPlanFromGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        UnshareMealPlanFromGroup command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        // No plan stream yet, or not currently shared with this exact group -- unsharing is
        // idempotent, same rationale as ClearMealSlotHandler's idempotent no-op.
        if (mealPlanId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents)!;

        if (plan.SharedWithGroupId != command.GroupId)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await mealPlans.AppendAsync(mealPlanId, [new MealPlanUnsharedFromGroup(mealPlanId, command.GroupId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
