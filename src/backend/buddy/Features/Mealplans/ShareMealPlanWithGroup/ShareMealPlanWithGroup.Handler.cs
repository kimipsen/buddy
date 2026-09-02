using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ShareMealPlanWithGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        ShareMealPlanWithGroup command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var mealplanAccess = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (mealplanAccess != MealplanAccess.Allowed)
        {
            return mealplanAccess.ToDeniedResult<Unit>();
        }

        // Sharing is a two-sided decision: the family's guardian and the group's own management
        // both have to consent, mirroring CreateCalendar's group-owned path.
        var group = Group.Rehydrate(await groups.ReadAsync(command.GroupId, cancellationToken));
        var groupAccess = GroupAuthorization.CheckManage(group, userId);

        if (groupAccess != GroupAccess.Allowed)
        {
            return groupAccess.ToDeniedResult<Unit>();
        }

        var now = DateTimeOffset.UtcNow;
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            var newId = MealPlanId.New();

            await mealPlans.CreateAsync(
                newId,
                [
                    new MealPlanCreated(newId, command.ChildId, now),
                    new MealPlanSharedWithGroup(newId, command.GroupId, command.ChildId, userId, now)
                ],
                cancellationToken);
        }
        else
        {
            var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
            var plan = MealPlan.Rehydrate(planEvents)!;

            // Already shared with this exact group -- idempotent no-op, same rationale as
            // UnshareMealPlanFromGroupHandler's already-not-shared check.
            if (plan.SharedWithGroupId == command.GroupId)
            {
                return new Result<Unit>.Success(Unit.Value);
            }

            await mealPlans.AppendAsync(mealPlanId, [new MealPlanSharedWithGroup(mealPlanId, command.GroupId, command.ChildId, userId, now)], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
