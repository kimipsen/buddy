using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ClearMealSlotForGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        ClearMealSlotForGroup command,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, Unit>();
        }

        return await ClearMealSlotHandler.ClearForChildAsync(access.AnchorChildId, command.Date, command.Slot, command.UserId!, mealPlans, guardians, cancellationToken);
    }
}
