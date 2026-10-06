using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

public static class ListMealPlanImportsHandler
{
    public static async Task<Result<IReadOnlyList<MealPlanImportSummary>>> Handle(
        ListMealPlanImports query,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyList<MealPlanImportSummary>>();
        }

        return await ListForChildAsync(query.ChildId, mealPlans, guardians, cancellationToken);
    }

    // Reads the event stream, not the snapshot: imports aren't aggregate state (see
    // MealPlanImportHistory).
    internal static async Task<Result<IReadOnlyList<MealPlanImportSummary>>> ListForChildAsync(
        UserId childId, IMealPlanEventStore mealPlans, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<IReadOnlyList<MealPlanImportSummary>>.Success([]);
        }

        var events = await mealPlans.ReadAsync(mealPlanId, cancellationToken);

        return new Result<IReadOnlyList<MealPlanImportSummary>>.Success(MealPlanImportHistory.Summarize(events));
    }
}

public static class ListMealPlanImportsForGroupHandler
{
    public static async Task<Result<IReadOnlyList<MealPlanImportSummary>>> Handle(
        ListMealPlanImportsForGroup query,
        IMealPlanEventStore mealPlans,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(query.GroupId, query.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, IReadOnlyList<MealPlanImportSummary>>();
        }

        return await ListMealPlanImportsHandler.ListForChildAsync(access.AnchorChildId, mealPlans, guardians, cancellationToken);
    }
}
