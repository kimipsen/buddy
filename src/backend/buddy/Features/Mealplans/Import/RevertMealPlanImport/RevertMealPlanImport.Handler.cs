using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Undoes an import without touching anything changed since: only slots this import was the last
// to write are cleared (MealPlanImportHistory.EntriesStillFrom), and only meals the import created that no
// slot uses any more are archived. Idempotent -- a second revert finds MealPlanImportReverted and
// writes nothing. See docs/backend/analysis/mealplan-import.md, Question 5.
public static class RevertMealPlanImportHandler
{
    public static async Task<Result<Unit>> Handle(
        RevertMealPlanImport command,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        return await RevertForChildAsync(command.ChildId, command.ImportId, command.UserId, mealPlans, meals, guardians, cancellationToken);
    }

    internal static async Task<Result<Unit>> RevertForChildAsync(
        UserId childId,
        MealPlanImportId importId,
        UserId revertedBy,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);

        if (mealPlanId is null)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await mealPlans.ReadAsync(mealPlanId, cancellationToken);

        if (MealPlanImportHistory.Find(events, importId) is not { } imported)
        {
            return new Result<Unit>.NotFound();
        }

        if (MealPlanImportHistory.IsReverted(events, importId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var now = DateTimeOffset.UtcNow;

        var cleared = MealPlanImportHistory.EntriesStillFrom(events, importId)
            .Select(entry => (MealPlanEvent)new MealSlotCleared(mealPlanId, entry.Date, entry.Slot, entry.Assignment, revertedBy, now))
            .ToList();

        var stillUsed = MealPlanImportHistory.MealsInUse(MealPlan.Replay([.. events, .. cleared]));
        var archived = new List<MealArchived>();

        foreach (var mealId in imported.CreatedMealIds.Where(id => !stillUsed.Contains(id)))
        {
            // ReadAsync, not the snapshot, so the archive append is an expected-version append.
            if (Meal.Rehydrate(await meals.ReadAsync(mealId, cancellationToken)) is { IsArchived: false })
            {
                archived.Add(new MealArchived(mealId, revertedBy, now));
            }
        }

        await mealPlans.RevertImportAsync(
            mealPlanId, [.. cleared, new MealPlanImportReverted(mealPlanId, importId, revertedBy, now)], archived, cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

public static class RevertMealPlanImportForGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        RevertMealPlanImportForGroup command,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, Unit>();
        }

        return await RevertMealPlanImportHandler.RevertForChildAsync(
            access.AnchorChildId, command.ImportId, command.UserId, mealPlans, meals, guardians, cancellationToken);
    }
}
