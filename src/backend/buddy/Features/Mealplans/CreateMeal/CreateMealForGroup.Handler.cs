using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class CreateMealForGroupHandler
{
    public static async Task<Result<Meal>> Handle(
        CreateMealForGroup command,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<Meal>.Validation("A meal requires a name.");
        }

        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, Meal>();
        }

        var meal = await CreateMealHandler.CreateForChildAsync(
            access.AnchorChildId, command.UserId!, command.Name, command.Description, command.Icon, command.Color, meals, cancellationToken);

        return new Result<Meal>.Success(meal);
    }
}
