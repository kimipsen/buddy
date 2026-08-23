using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ClearMealSlotHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearMealSlot command,
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

        // No plan stream at all yet, or nothing assigned at this slot -- clearing is idempotent,
        // so a guardian double-tapping "clear" isn't an error.
        if (mealPlanId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents)!;

        if (plan.Assignments.GetValueOrDefault((command.Date, command.Slot)) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await mealPlans.AppendAsync(mealPlanId, [new MealSlotCleared(mealPlanId, command.Date, command.Slot, before, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
