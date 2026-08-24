using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotForGroupHandler
{
    public static async Task<Result<MealPlanEntry>> Handle(
        AssignMealToSlotForGroup command,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, MealPlanEntry>();
        }

        return await AssignMealToSlotHandler.AssignForChildAsync(
            access.AnchorChildId, command.Date, command.Slot, command.MealId, command.Notes, command.UserId!, mealPlans, meals, guardians, cancellationToken);
    }
}
