using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        return await ClearForChildAsync(command.ChildId, command.Date, command.Slot, userId, mealPlans, guardians, cancellationToken);
    }

    // Shared with ClearMealSlotForGroupHandler -- see CreateMealHandler.CreateForChildAsync for
    // the same pattern and rationale.
    internal static async Task<Result<Unit>> ClearForChildAsync(
        UserId childId, DateOnly date, MealSlot slot, UserId modifiedBy, IMealPlanEventStore mealPlans, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);

        // No plan stream at all yet, or nothing assigned at this slot -- clearing is idempotent,
        // so a guardian double-tapping "clear" isn't an error.
        if (mealPlanId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
        var plan = MealPlan.Rehydrate(planEvents)!;

        if (plan.Assignments.GetValueOrDefault((date, slot)) is not { } before)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await mealPlans.AppendAsync(mealPlanId, [new MealSlotCleared(mealPlanId, date, slot, before, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
