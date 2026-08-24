using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        var meal = await CreateForChildAsync(command.ChildId, userId, command.Name, command.Description, command.Icon, command.Color, meals, cancellationToken);

        return new Result<Meal>.Success(meal);
    }

    // Shared with CreateMealForGroupHandler, which resolves its own AnchorChildId/actingUserId
    // through a group's MealplanPermissionPolicy instead of MealplanAuthorization -- everything
    // past authorization is identical (see docs/backend/analysis/group-owned-mealplans.md). The
    // new meal is indexed under childId regardless of which route created it, same as any
    // guardian-created meal.
    internal static async Task<Meal> CreateForChildAsync(
        UserId childId, UserId createdBy, string name, string? description, Icon icon, Color color, IMealEventStore meals, CancellationToken cancellationToken)
    {
        var mealId = MealId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new MealCreated(mealId, childId, createdBy, name, description, icon, color, now);

        var events = await meals.CreateAsync(mealId, [created], cancellationToken);

        return Meal.Rehydrate(events)!;
    }
}
