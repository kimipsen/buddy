using FluentValidation;

namespace buddy.Features.Mealplans;

public static class CommitMealPlanImportRules
{
    // Ten years of dinners. One commit is one transaction and one MealPlanEntriesImported, so the
    // client never has to split an import (splitting would create the same new meal once per
    // part). A larger history goes in several imports, one per few years.
    public const int MaxEntries = 5000;

    // The text formats, plus "mealie" (docs/backend/analysis/mealie-integration.md, Question 5).
    public static bool IsKnownFormat(string format) =>
        MealPlanImportFormats.Find(format) is not null || IsMealieFormat(format);

    public static bool IsMealieFormat(string format) =>
        string.Equals(format, MealieImportSource.FormatId, StringComparison.OrdinalIgnoreCase);

    public static bool HasNoMealieRecipe(IReadOnlyList<MealPlanImportEntry> entries) =>
        entries.All(entry => entry.MealieRecipe is null);

    public static bool HasUniqueSlots(IReadOnlyList<MealPlanImportEntry> entries) =>
        entries.Select(entry => (entry.Date, entry.Slot)).Distinct().Count() == entries.Count;
}

public sealed class MealPlanImportEntryValidator : AbstractValidator<MealPlanImportEntry>
{
    public MealPlanImportEntryValidator()
    {
        RuleFor(e => e.Slot).IsInEnum();
        RuleFor(e => e.Notes).MaximumLength(ImportLineClassifier.MaxNotesLength);
        RuleFor(e => e.NewMealName).MaximumLength(ImportLineClassifier.MaxMealNameLength);
        RuleFor(e => e.NewMealName)
            .Must((entry, name) => entry.MealId is null != (name.Trim().Length == 0))
            .WithMessage("Each entry needs either a mealId or a newMealName, not both.");
        RuleFor(e => e.MealieRecipe)
            .Null()
            .When(e => e.MealId is not null)
            .WithMessage("A mealieRecipe only links a new meal; leave it out for an existing one.");
        RuleFor(e => e.MealieRecipe!.RecipeId).NotEmpty().When(e => e.MealieRecipe is not null).OverridePropertyName("mealieRecipe.recipeId");
        RuleFor(e => e.MealieRecipe!.Name).MaximumLength(ImportLineClassifier.MaxMealNameLength).When(e => e.MealieRecipe is not null).OverridePropertyName("mealieRecipe.name");
        RuleFor(e => e.MealieRecipe!.Slug).MaximumLength(ImportLineClassifier.MaxMealNameLength).When(e => e.MealieRecipe is not null).OverridePropertyName("mealieRecipe.slug");
    }
}

public sealed class CommitMealPlanImportValidator : AbstractValidator<CommitMealPlanImport>
{
    public CommitMealPlanImportValidator()
    {
        RuleFor(x => x.Format).Must(CommitMealPlanImportRules.IsKnownFormat).WithMessage("Unknown import format.");
        RuleFor(x => x.Entries)
            .Must(CommitMealPlanImportRules.HasNoMealieRecipe)
            .When(x => !CommitMealPlanImportRules.IsMealieFormat(x.Format))
            .WithMessage("Only a Mealie import can link meals to Mealie recipes.");
        RuleFor(x => x.Entries)
            .NotEmpty()
            .Must(e => e.Count <= CommitMealPlanImportRules.MaxEntries)
            .WithMessage($"An import can hold at most {CommitMealPlanImportRules.MaxEntries} entries; split it into several imports.")
            .Must(CommitMealPlanImportRules.HasUniqueSlots)
            .WithMessage("Each date and slot can appear only once in an import.");
        RuleForEach(x => x.Entries).SetValidator(new MealPlanImportEntryValidator());
    }
}

public sealed class CommitMealPlanImportForGroupValidator : AbstractValidator<CommitMealPlanImportForGroup>
{
    public CommitMealPlanImportForGroupValidator()
    {
        RuleFor(x => x.Format).Must(CommitMealPlanImportRules.IsKnownFormat).WithMessage("Unknown import format.");
        RuleFor(x => x.Entries)
            .Must(CommitMealPlanImportRules.HasNoMealieRecipe)
            .When(x => !CommitMealPlanImportRules.IsMealieFormat(x.Format))
            .WithMessage("Only a Mealie import can link meals to Mealie recipes.");
        RuleFor(x => x.Entries)
            .NotEmpty()
            .Must(e => e.Count <= CommitMealPlanImportRules.MaxEntries)
            .WithMessage($"An import can hold at most {CommitMealPlanImportRules.MaxEntries} entries; split it into several imports.")
            .Must(CommitMealPlanImportRules.HasUniqueSlots)
            .WithMessage("Each date and slot can appear only once in an import.");
        RuleForEach(x => x.Entries).SetValidator(new MealPlanImportEntryValidator());
    }
}
