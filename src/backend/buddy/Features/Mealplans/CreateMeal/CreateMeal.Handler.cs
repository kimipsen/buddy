using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class CreateMealHandler
{
    public static async Task<Result<Meal>> Handle(
        CreateMeal command,
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

        var mealId = MealId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new MealCreated(mealId, command.ChildId, userId, command.Name, command.Description, command.Icon, command.Color, now);

        var events = await meals.CreateAsync(mealId, [created], cancellationToken);

        return new Result<Meal>.Success(Meal.Rehydrate(events)!);
    }
}
