using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

public static class ListMealPlanForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<MealPlanEntry>>> Handle(
        ListMealPlanForGroup query,
        IValidator<ListMealPlanForGroup> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(query, cancellationToken) is { } problem)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation(problem);
        }

        var resolved = await MealplanGroupAccess.ResolveViewAsync(query.GroupId, query.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, IReadOnlyCollection<MealPlanEntry>>();
        }

        var entries = await MealPlanExpansion.ExpandAsync(access.AnchorChildId, query.From, query.To, mealPlans, meals, guardians, cancellationToken);

        return new Result<IReadOnlyCollection<MealPlanEntry>>.Success(entries);
    }
}
