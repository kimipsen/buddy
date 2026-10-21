using System.Collections.Immutable;

using buddy.Common.Validation;
using buddy.Features.Guardians;

using FluentValidation;

namespace buddy.Features.Mealplans;

// Fetches the household's Mealie plan for the range and builds the same review the text import
// shows. Writes nothing; CommitMealPlanImport writes it with format "mealie".
public static class PreviewMealieImportHandler
{
    public static async Task<MealieOutcome<MealieImportPreview>> Handle(
        PreviewMealieImport command,
        IValidator<PreviewMealieImport> validator,
        IMealieConnectionEventStore connections,
        IGuardianLinkEventStore guardians,
        IMealEventStore meals,
        IMealPlanEventStore mealPlans,
        IMealieTokenCipher cipher,
        IMealieClient mealie,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new MealieOutcome<MealieImportPreview>.Validation(problem);
        }

        var access = await MealplanAuthorization.CheckManage(command.ChildId, command.UserId, guardians, cancellationToken);

        if (access != MealplanAccess.Allowed)
        {
            return MealieOutcome.Denied<MealieImportPreview>(access);
        }

        var resolved = await MealieConnectionResolution.ResolveAsync(command.ChildId, guardians, connections, cipher, cancellationToken);

        if (resolved is not MealieOutcome<ActiveMealieConnection>.Success(var connection))
        {
            return resolved.Reraise<ActiveMealieConnection, MealieImportPreview>();
        }

        var fetched = await mealie.ListMealPlanAsync(connection.Endpoint, command.From, command.To, cancellationToken);

        if (fetched is not MealieResponse<IReadOnlyList<MealiePlanEntryDto>>.Ok(var entries))
        {
            // The plan route itself is never "not found"; a 404 there means something else is wrong.
            var failure = fetched.FailureOf();
            return new MealieOutcome<MealieImportPreview>.UpstreamFailed(failure.Kind == MealieFailureKind.NotFound ? failure with { Kind = MealieFailureKind.Error } : failure);
        }

        var familyMeals = await ListMealsHandler.LoadFamilyMealsAsync(command.ChildId, meals, guardians, cancellationToken);
        var source = MealieImportSource.Build(entries, familyMeals);

        var mealPlanId = await MealFamilyResolution.ResolveFamilyMealPlanIdAsync(command.ChildId, guardians, mealPlans, cancellationToken);
        var plan = mealPlanId is null ? null : await mealPlans.FindSnapshotAsync(mealPlanId, cancellationToken);
        var assignments = plan?.Assignments ?? ImmutableDictionary<(DateOnly Date, MealSlot Slot), MealPlanAssignment>.Empty;

        var preview = MealPlanImportPreviewBuilder.Build(MealieImportSource.FormatId, source.Parsed, familyMeals, assignments);

        return new MealieOutcome<MealieImportPreview>.Success(new MealieImportPreview(
            preview,
            [.. source.RecipesByKey.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new MealieImportRecipe(pair.Key, pair.Value))]));
    }
}
