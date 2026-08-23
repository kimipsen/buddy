using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotHandler
{
    public static async Task<Result<MealPlanEntry>> Handle(
        AssignMealToSlot command,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealPlanEntry>();
        }

        var mealEvents = await meals.ReadAsync(command.MealId, cancellationToken);
        var meal = Meal.Rehydrate(mealEvents);

        if (meal is null)
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(command.ChildId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(command.MealId))
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        if (meal.IsArchived)
        {
            return new Result<MealPlanEntry>.Validation("Cannot assign an archived meal.");
        }

        var after = new MealPlanAssignment(command.MealId, userId, command.Notes);
        var now = DateTimeOffset.UtcNow;

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            var newId = MealPlanId.New();

            await mealPlans.CreateAsync(
                newId,
                [
                    new MealPlanCreated(newId, command.ChildId, now),
                    new MealAssignedToSlot(newId, command.Date, command.Slot, Before: null, after, now)
                ],
                cancellationToken);
        }
        else
        {
            var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
            var plan = MealPlan.Rehydrate(planEvents)!;
            var before = plan.Assignments.GetValueOrDefault((command.Date, command.Slot));

            if (before is null || before.MealId != after.MealId || before.Notes != after.Notes)
            {
                await mealPlans.AppendAsync(mealPlanId, [new MealAssignedToSlot(mealPlanId, command.Date, command.Slot, before, after, now)], cancellationToken);
            }
        }

        return new Result<MealPlanEntry>.Success(new MealPlanEntry(
            command.Date, command.Slot, meal.Id, meal.Name, meal.Icon.Value, meal.Color.Value,
            meal.Ratings.GetValueOrDefault(command.ChildId), command.Notes, userId.Value));
    }
}
