using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class UpdateMealDetailsForGroupHandler
{
    public static async Task<Result<Meal>> Handle(
        UpdateMealDetailsForGroup command,
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

        return await UpdateMealDetailsHandler.UpdateForChildAsync(
            access.AnchorChildId, command.MealId, command.UserId!, command.Name, command.Description, command.Icon, command.Color, meals, guardians, cancellationToken);
    }
}
