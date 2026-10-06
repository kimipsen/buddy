using System.Collections.Immutable;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed record SkippedImportEntry(DateOnly Date, MealSlot Slot, string Reason);

public sealed record MealPlanImportResult(
    MealPlanImportId? ImportId,
    int Imported,
    int CreatedMeals,
    int ArchivedMeals,
    IReadOnlyList<SkippedImportEntry> Skipped);

// Writes a reviewed import -- see docs/backend/analysis/mealplan-import.md, Question 5.
public static class CommitMealPlanImportHandler
{
    public const string SkippedOccupied = "occupied";

    // Meals created by the import with fewer uses than this, none of them within
    // SingleUseRecentDays of today, are archived straight away when ArchiveSingleUse is set.
    private const int SingleUseThreshold = 2;
    private const int SingleUseRecentDays = 90;

    // The same default the Manage meals form preselects (manage-meals.ts); editable afterwards.
    private static readonly Icon DefaultIcon = Icon.New("🍽️");
    private static readonly Color DefaultColor = Color.New("#10b981");

    public static async Task<Result<MealPlanImportResult>> Handle(
        CommitMealPlanImport command,
        IValidator<CommitMealPlanImport> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanImportResult>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealPlanImportResult>();
        }

        return await ImportForChildAsync(
            command.ChildId, command.UserId, command.Format, command.Entries, command.ArchiveSingleUse, mealPlans, meals, guardians, cancellationToken);
    }

    // Shared with CommitMealPlanImportForGroupHandler -- see CreateMealHandler.CreateForChildAsync.
    internal static async Task<Result<MealPlanImportResult>> ImportForChildAsync(
        UserId childId,
        UserId importedBy,
        string format,
        IReadOnlyList<MealPlanImportEntry> entries,
        bool archiveSingleUse,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var familyMealIds = await MealFamilyResolution.ResolveFamilyMealIdsAsync(childId, guardians, meals, cancellationToken);
        var unknown = entries.Where(e => e.MealId is { } id && !familyMealIds.Contains(id)).Select(e => e.MealId).FirstOrDefault();

        if (unknown is not null)
        {
            // Archived meals are fine here: they're exactly how an earlier import keeps one-off
            // dishes out of the picker, and history may point at them.
            return new Result<MealPlanImportResult>.Validation(ValidationProblem.Of($"Meal {unknown.Value} is not in this family's meal library."));
        }

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);
        var occupied = mealPlanId is null
            ? ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>.Empty
            : MealPlan.Replay(await mealPlans.ReadAsync(mealPlanId, cancellationToken)).Assignments;

        var skipped = entries
            .Where(e => occupied.ContainsKey((e.Date, e.Slot)))
            .Select(e => new SkippedImportEntry(e.Date, e.Slot, SkippedOccupied))
            .ToList();
        var toImport = entries.Where(e => !occupied.ContainsKey((e.Date, e.Slot))).ToList();

        if (toImport.Count == 0)
        {
            return new Result<MealPlanImportResult>.Success(new MealPlanImportResult(null, 0, 0, 0, skipped));
        }

        var now = DateTimeOffset.UtcNow;
        var recentCutoff = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-SingleUseRecentDays);

        // One new meal per normalized name, named after its first spelling.
        var newMeals = toImport
            .Where(e => e.MealId is null)
            .GroupBy(e => ImportLineClassifier.NormalizeKey(e.NewMealName))
            .Select(g => (
                Key: g.Key,
                Id: MealId.New(),
                Name: g.First().NewMealName.Trim(),
                Archive: archiveSingleUse && g.Count() < SingleUseThreshold && g.Max(e => e.Date) < recentCutoff))
            .ToList();
        var newMealIdsByKey = newMeals.ToDictionary(m => m.Key, m => m.Id);

        var mealEvents = newMeals
            .Select(m => (IReadOnlyCollection<MealEvent>)(m.Archive
                ? [new MealCreated(m.Id, childId, importedBy, m.Name, "", DefaultIcon, DefaultColor, now), new MealArchived(m.Id, importedBy, now)]
                : [new MealCreated(m.Id, childId, importedBy, m.Name, "", DefaultIcon, DefaultColor, now)]))
            .ToList();

        var importedEntries = toImport
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Slot)
            .Select(e => new ImportedMealPlanEntry(
                e.Date,
                e.Slot,
                new MealPlanAssignment(e.MealId ?? newMealIdsByKey[ImportLineClassifier.NormalizeKey(e.NewMealName)], importedBy, e.Notes.Trim())))
            .ToImmutableArray();

        var importId = MealPlanImportId.New();
        var planId = mealPlanId ?? MealPlanId.New();
        var imported = new MealPlanEntriesImported(
            planId, importId, MealPlanImportFormats.Find(format)?.Id ?? format, importedEntries, [.. newMeals.Select(m => m.Id)], importedBy, now);

        IReadOnlyCollection<MealPlanEvent> planEvents = mealPlanId is null
            ? [new MealPlanCreated(planId, childId, now), imported]
            : [imported];

        await mealPlans.ImportAsync(planId, planEvents, mealEvents, cancellationToken);

        return new Result<MealPlanImportResult>.Success(new MealPlanImportResult(
            importId, importedEntries.Length, newMeals.Count, newMeals.Count(m => m.Archive), skipped));
    }
}

public static class CommitMealPlanImportForGroupHandler
{
    public static async Task<Result<MealPlanImportResult>> Handle(
        CommitMealPlanImportForGroup command,
        IValidator<CommitMealPlanImportForGroup> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanImportResult>.Validation(problem);
        }

        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, MealPlanImportResult>();
        }

        return await CommitMealPlanImportHandler.ImportForChildAsync(
            access.AnchorChildId, command.UserId, command.Format, command.Entries, command.ArchiveSingleUse, mealPlans, meals, guardians, cancellationToken);
    }
}
