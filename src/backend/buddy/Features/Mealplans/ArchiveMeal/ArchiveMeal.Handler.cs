using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ArchiveMealHandler
{
    public static async Task<Result<Unit>> Handle(
        ArchiveMeal command,
        IMealEventStore meals,
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

        var events = await meals.ReadAsync(command.MealId, cancellationToken);
        var meal = Meal.Rehydrate(events);

        if (meal is null || meal.IsArchived)
        {
            return new Result<Unit>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(command.ChildId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(command.MealId))
        {
            return new Result<Unit>.NotFound();
        }

        await meals.AppendAsync(command.MealId, [new MealArchived(command.MealId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
