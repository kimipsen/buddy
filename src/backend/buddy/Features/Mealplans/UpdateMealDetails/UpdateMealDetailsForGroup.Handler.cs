using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class UpdateMealDetailsForGroupHandler
{
    public static async Task<Result<Meal>> Handle(
        UpdateMealDetailsForGroup command,
        IValidator<UpdateMealDetailsForGroup> validator,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<Meal>.Validation(problem);
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
