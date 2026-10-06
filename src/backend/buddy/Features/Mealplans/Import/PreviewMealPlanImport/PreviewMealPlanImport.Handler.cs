using System.Collections.Immutable;

using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Mealplans;

// Parses and matches; writes nothing -- see docs/backend/analysis/mealplan-import.md, Question 2.
public static class PreviewMealPlanImportHandler
{
    public static async Task<Result<MealPlanImportPreview>> Handle(
        PreviewMealPlanImport command,
        IValidator<PreviewMealPlanImport> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanImportPreview>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return access.ToDeniedResult<MealPlanImportPreview>();
        }

        return await PreviewForChildAsync(command.ChildId, command.Text, command.Format, command.Options, mealPlans, meals, guardians, cancellationToken);
    }

    // Shared with PreviewMealPlanImportForGroupHandler -- see CreateMealHandler.CreateForChildAsync.
    internal static async Task<Result<MealPlanImportPreview>> PreviewForChildAsync(
        UserId childId, string text, string formatId, MealPlanImportOptions options,
        IMealPlanEventStore mealPlans, IMealEventStore meals, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var format = formatId == MealPlanImportFormats.Auto ? MealPlanImportFormats.Detect(text) : MealPlanImportFormats.Find(formatId);

        if (format is null)
        {
            return new Result<MealPlanImportPreview>.Validation(ValidationProblem.Of(
                "The text doesn't look like a known import format. Choose the format explicitly."));
        }

        var parseResult = format.Parse(text, options);

        if (parseResult is not Result<ParsedImport>.Success(var parsed))
        {
            return parseResult.Reraise<ParsedImport, MealPlanImportPreview>();
        }

        var familyMeals = await ListMealsHandler.LoadFamilyMealsAsync(childId, meals, guardians, cancellationToken);
        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(childId, guardians, mealPlans, cancellationToken);
        var plan = mealPlanId is null ? null : await mealPlans.FindSnapshotAsync(mealPlanId, cancellationToken);
        var assignments = plan?.Assignments ?? ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>.Empty;

        return new Result<MealPlanImportPreview>.Success(MealPlanImportPreviewBuilder.Build(format.Id, parsed, familyMeals, assignments));
    }
}

public static class PreviewMealPlanImportForGroupHandler
{
    public static async Task<Result<MealPlanImportPreview>> Handle(
        PreviewMealPlanImportForGroup command,
        IValidator<PreviewMealPlanImportForGroup> validator,
        IMealPlanEventStore mealPlans,
        IMealEventStore meals,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MealPlanImportPreview>.Validation(problem);
        }

        var resolved = await MealplanGroupAccess.ResolveManageAsync(command.GroupId, command.UserId, groups, mealPlans, cancellationToken);

        if (resolved is not Result<MealplanGroupAccess.Resolved>.Success(var access))
        {
            return resolved.Reraise<MealplanGroupAccess.Resolved, MealPlanImportPreview>();
        }

        return await PreviewMealPlanImportHandler.PreviewForChildAsync(
            access.AnchorChildId, command.Text, command.Format, command.Options, mealPlans, meals, guardians, cancellationToken);
    }
}
