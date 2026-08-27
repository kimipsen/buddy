using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class CreateMealForGroupHandler
{
    public static async Task<Result<Meal>> Handle(
        CreateMealForGroup command,
        IValidator<CreateMealForGroup> validator,
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

        var meal = await CreateMealHandler.CreateForChildAsync(
            access.AnchorChildId, command.UserId!, command.Name, command.Description, command.Icon, command.Color, meals, cancellationToken);

        return new Result<Meal>.Success(meal);
    }
}
