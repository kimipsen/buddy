using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListMealPlanForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<MealPlanEntry>>> Handle(
        ListMealPlanForGroup query,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (query.To < query.From)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation("'to' must not be before 'from'.");
        }

        if (query.To.DayNumber - query.From.DayNumber > ListMealPlanHandler.MaxRangeDays)
        {
            return new Result<IReadOnlyCollection<MealPlanEntry>>.Validation($"The requested range cannot exceed {ListMealPlanHandler.MaxRangeDays} days.");
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
