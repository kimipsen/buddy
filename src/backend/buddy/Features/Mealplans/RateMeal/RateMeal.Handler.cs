using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class RateMealHandler
{
    public static async Task<Result<Meal>> Handle(
        RateMeal command,
        IValidator<RateMeal> validator,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Meal>.Validation(problem);
        }

        if (command.UserId is not { } userId)
        {
            return new Result<Meal>.NotFound();
        }

        var access = await MealplanAuthorization.CheckRate(command.ChildId, userId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Meal>();
        }

        var events = await meals.ReadAsync(command.MealId, cancellationToken);
        var meal = Meal.Rehydrate(events);

        // Deliberately allowed even when the Meal is archived -- an opinion of the dish doesn't
        // depend on whether it's still in active rotation.
        if (meal is null)
        {
            return new Result<Meal>.NotFound();
        }

        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(command.ChildId, guardians, meals, cancellationToken);

        if (!familyMealIds.Contains(command.MealId))
        {
            return new Result<Meal>.NotFound();
        }

        var before = meal.Ratings.GetValueOrDefault(userId);
        var after = new MealRating(command.Stars, command.Comment, DateTimeOffset.UtcNow);

        await meals.AppendAsync(command.MealId, [new MealRated(command.MealId, userId, before, after, after.RatedAt)], cancellationToken);

        return new Result<Meal>.Success(meal with { Ratings = meal.Ratings.SetItem(userId, after), LastModifiedBy = userId });
    }
}
