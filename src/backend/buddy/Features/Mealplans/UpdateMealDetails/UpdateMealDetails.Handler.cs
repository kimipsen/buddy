using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class UpdateMealDetailsHandler
{
    public static async Task<Result<Meal>> Handle(
        UpdateMealDetails command,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<Meal>.Validation("A meal requires a name.");
        }

        if (command.UserId is not { } userId)
        {
            return new Result<Meal>.NotFound();
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Meal>();
        }

        var events = await meals.ReadAsync(command.MealId, cancellationToken);
        var meal = Meal.Rehydrate(events);

        if (meal is null || meal.IsArchived)
        {
            return new Result<Meal>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(command.ChildId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(command.MealId))
        {
            return new Result<Meal>.NotFound();
        }

        var before = new MealDetails(meal.Name, meal.Description, meal.Icon, meal.Color);
        var after = new MealDetails(command.Name, command.Description, command.Icon, command.Color);

        if (before == after)
        {
            return new Result<Meal>.Success(meal);
        }

        await meals.AppendAsync(command.MealId, [new MealDetailsUpdated(command.MealId, before, after, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Meal>.Success(meal with { Name = command.Name, Description = command.Description, Icon = command.Icon, Color = command.Color, LastModifiedBy = userId });
    }
}
