using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Mealplans;

public static class ListMealsForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<Meal>>> Handle(
        ListMealsForGroup query,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(query.GroupId, query.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, IReadOnlyCollection<Meal>>();
        }

        var loaded = await ListMealsHandler.LoadFamilyMealsAsync(access.AnchorChildId, meals, guardians, cancellationToken);

        return new Result<IReadOnlyCollection<Meal>>.Success(loaded);
    }
}
