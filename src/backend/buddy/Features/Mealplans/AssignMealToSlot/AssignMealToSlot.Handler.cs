using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotHandler
{
    public static async Task<Result<MealPlanEntry>> Handle(
        AssignMealToSlot command,
        IValidator<AssignMealToSlot> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanEntry>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealPlanEntry>();
        }

        return await AssignForChildAsync(command.ChildId, command.Date, command.Slot, command.MealId, command.Notes, userId, mealPlans, meals, guardians, cancellationToken);
    }

    // Shared with AssignMealToSlotForGroupHandler -- see CreateMealHandler.CreateForChildAsync
    // for the same pattern and rationale.
    internal static async Task<Result<MealPlanEntry>> AssignForChildAsync(
        UserId childId, DateOnly date, MealSlot slot, MealId mealId, string? notes, UserId assignedBy,
        IMealPlanEventStore mealPlans, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var mealEvents = await meals.ReadAsync(mealId, cancellationToken);
        var meal = Meal.Rehydrate(mealEvents);

        if (meal is null)
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(mealId))
        {
            return new Result<MealPlanEntry>.NotFound();
        }

        if (meal.IsArchived)
        {
            // State-dependent (needs the loaded Meal aggregate), so this stays as handler code
            // rather than a FluentValidation rule -- see AssignPickup.ValidateRelationshipAsync
            // for the same reasoning.
            return new Result<MealPlanEntry>.Validation(ValidationProblem.Of("Cannot assign an archived meal."));
        }

        var after = new MealPlanAssignment(mealId, assignedBy, notes);
        var now = DateTimeOffset.UtcNow;

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            var newId = MealPlanId.New();

            await mealPlans.CreateAsync(
                newId,
                [
                    new MealPlanCreated(newId, childId, now),
                    new MealAssignedToSlot(newId, date, slot, Before: null, after, now)
                ],
                cancellationToken);
        }
        else
        {
            var planEvents = await mealPlans.ReadAsync(mealPlanId, cancellationToken);
            var plan = MealPlan.Rehydrate(planEvents)!;
            var before = plan.Assignments.GetValueOrDefault((date, slot));

            if (before is null || before.MealId != after.MealId || before.Notes != after.Notes)
            {
                await mealPlans.AppendAsync(mealPlanId, [new MealAssignedToSlot(mealPlanId, date, slot, before, after, now)], cancellationToken);
            }
        }

        return new Result<MealPlanEntry>.Success(new MealPlanEntry(
            date, slot, meal.Id, meal.Name, meal.Icon.Value, meal.Color.Value,
            meal.Ratings.GetValueOrDefault(childId), notes, assignedBy.Value,
            [.. meal.Ratings.Select(pair => new MealPlanEntryRating(pair.Key, pair.Value.Stars, pair.Value.Comment, pair.Value.RatedAt))]));
    }
}
