using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Mealplans;

public sealed record GroupMealplanStatus(bool HasSharedPlan);

// Lets the frontend tell, ahead of rendering a group's meal-plan tab, whether any family has
// actually shared a plan with that group -- reuses the exact same authorize-then-look-up check
// every other group-keyed read already performs (MealplanGroupAccess.ResolveViewAsync), just
// turning its NotFound ("nothing shared yet") into a normal false result instead of an error.
public static class GetGroupMealplanStatusHandler
{
    public static async Task<Result<GroupMealplanStatus>> Handle(
        GetGroupMealplanStatus query, IGroupEventStore groups, IMealPlanEventStore mealPlans, CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveViewAsync(query.GroupId, query.UserId, groups, mealPlans, cancellationToken);

        return resolved switch
        {
            Result<MealplanGroupAccess.Resolved>.Success => new Result<GroupMealplanStatus>.Success(new GroupMealplanStatus(true)),
            Result<MealplanGroupAccess.Resolved>.NotFound => new Result<GroupMealplanStatus>.Success(new GroupMealplanStatus(false)),
            _ => resolved.Reraise<MealplanGroupAccess.Resolved, GroupMealplanStatus>(),
        };
    }
}
