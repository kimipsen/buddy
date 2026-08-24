using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Resolves *which* plan a group-keyed request should operate on: authorize the caller against
// the group's policy, then look up the plan the family shared with that group. Every group-keyed
// handler calls this once and then reuses the exact same post-authorization logic the
// child-keyed handler already has, parametrized by the resolved AnchorChildId (see
// docs/backend/analysis/group-owned-mealplans.md).
public static class MealplanGroupAccess
{
    public sealed record Resolved(MealPlanId MealPlanId, UserId AnchorChildId);

    // Read-only group-keyed endpoints (ListMealPlanForGroup, ListMealsForGroup): a View or
    // Manage tier both grant access.
    public static Task<Result<Resolved>> ResolveViewAsync(
        GroupId groupId, UserId? callerId, IGroupEventStore groups, IMealPlanEventStore mealPlans, CancellationToken cancellationToken) =>
        ResolveAsync(groupId, callerId, groups, mealPlans, MealplanGroupAuthorization.CheckView, cancellationToken);

    // Write group-keyed endpoints: only Manage tier grants access; a View-tier caller resolves to
    // Forbidden (they can see the plan exists, just not write to it).
    public static Task<Result<Resolved>> ResolveManageAsync(
        GroupId groupId, UserId? callerId, IGroupEventStore groups, IMealPlanEventStore mealPlans, CancellationToken cancellationToken) =>
        ResolveAsync(groupId, callerId, groups, mealPlans, MealplanGroupAuthorization.CheckManage, cancellationToken);

    private static async Task<Result<Resolved>> ResolveAsync(
        GroupId groupId,
        UserId? callerId,
        IGroupEventStore groups,
        IMealPlanEventStore mealPlans,
        Func<GroupId, UserId, IGroupEventStore, CancellationToken, Task<MealplanAccess>> check,
        CancellationToken cancellationToken)
    {
        if (callerId is not { } userId)
        {
            return new Result<Resolved>.NotFound();
        }

        var access = await check(groupId, userId, groups, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Resolved>();
        }

        var shared = await mealPlans.FindGroupSharedAsync(groupId, cancellationToken);

        if (shared is null)
        {
            return new Result<Resolved>.NotFound();
        }

        return new Result<Resolved>.Success(new Resolved(new MealPlanId(shared.Id), new UserId(shared.AnchorChildId)));
    }
}
