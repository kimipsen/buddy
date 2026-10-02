using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class AssignMealToSlotForGroupHandler
{
    public static async Task<Result<MealPlanEntry>> Handle(
        AssignMealToSlotForGroup command,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        IValidator<AssignMealToSlotForGroup> validator,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanEntry>.Validation(problem);
        }

        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, MealPlanEntry>();
        }

        return await AssignMealToSlotHandler.AssignForChildAsync(
            access.AnchorChildId, command.Date, command.Slot, command.MealId, command.Notes, command.UserId, mealPlans, meals, guardians, cancellationToken);
    }
}
