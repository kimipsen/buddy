using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        return await UpdateForChildAsync(command.ChildId, command.MealId, userId, command.Name, command.Description, command.Icon, command.Color, meals, guardians, cancellationToken);
    }

    // Shared with UpdateMealDetailsForGroupHandler -- see CreateMealHandler.CreateForChildAsync
    // for the same pattern and rationale.
    internal static async Task<Result<Meal>> UpdateForChildAsync(
        UserId childId, MealId mealId, UserId modifiedBy, string name, string? description, Icon icon, Color color,
        IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var events = await meals.ReadAsync(mealId, cancellationToken);
        var meal = Meal.Rehydrate(events);

        if (meal is null || meal.IsArchived)
        {
            return new Result<Meal>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(mealId))
        {
            return new Result<Meal>.NotFound();
        }

        var before = new MealDetails(meal.Name, meal.Description, meal.Icon, meal.Color);
        var after = new MealDetails(name, description, icon, color);

        if (before == after)
        {
            return new Result<Meal>.Success(meal);
        }

        await meals.AppendAsync(mealId, [new MealDetailsUpdated(mealId, before, after, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Meal>.Success(meal with { Name = name, Description = description, Icon = icon, Color = color, LastModifiedBy = modifiedBy });
    }
}
