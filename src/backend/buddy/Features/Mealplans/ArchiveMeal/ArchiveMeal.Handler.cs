using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        return await ArchiveForChildAsync(command.ChildId, command.MealId, userId, meals, guardians, cancellationToken);
    }

    // Shared with ArchiveMealForGroupHandler -- see CreateMealHandler.CreateForChildAsync for the
    // same pattern and rationale.
    internal static async Task<Result<Unit>> ArchiveForChildAsync(
        UserId childId, MealId mealId, UserId modifiedBy, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var events = await meals.ReadAsync(mealId, cancellationToken);
        var meal = Meal.Rehydrate(events);

        if (meal is null || meal.IsArchived)
        {
            return new Result<Unit>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(mealId))
        {
            return new Result<Unit>.NotFound();
        }

        await meals.AppendAsync(mealId, [new MealArchived(mealId, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
