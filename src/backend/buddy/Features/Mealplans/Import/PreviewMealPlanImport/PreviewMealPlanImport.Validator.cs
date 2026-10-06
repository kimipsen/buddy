using FluentValidation;

namespace buddy.Features.Mealplans;

public static class PreviewMealPlanImportRules
{
    // ~256 KB of text: three years of a weekly note is ~40 KB.
    public const int MaxTextLength = 256 * 1024;

    public static bool IsKnownFormat(string format) =>
        format == MealPlanImportFormats.Auto || MealPlanImportFormats.Find(format) is not null;
}

public sealed class PreviewMealPlanImportValidator : AbstractValidator<PreviewMealPlanImport>
{
    public PreviewMealPlanImportValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(PreviewMealPlanImportRules.MaxTextLength);
        RuleFor(x => x.Format).Must(PreviewMealPlanImportRules.IsKnownFormat).WithMessage("Unknown import format.");
        RuleFor(x => x.Options.WeekStart).IsInEnum();
        RuleFor(x => x.Options.Slot).IsInEnum();
    }
}

public sealed class PreviewMealPlanImportForGroupValidator : AbstractValidator<PreviewMealPlanImportForGroup>
{
    public PreviewMealPlanImportForGroupValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(PreviewMealPlanImportRules.MaxTextLength);
        RuleFor(x => x.Format).Must(PreviewMealPlanImportRules.IsKnownFormat).WithMessage("Unknown import format.");
        RuleFor(x => x.Options.WeekStart).IsInEnum();
        RuleFor(x => x.Options.Slot).IsInEnum();
    }
}
