using FluentValidation;

namespace buddy.Features.Mealplans;

public sealed class PreviewMealieImportValidator : AbstractValidator<PreviewMealieImport>
{
    // One quarter: a few Mealie pages. Older history goes through several previews.
    public const int MaxDays = 92;

    public PreviewMealieImportValidator()
    {
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("'To' must be on or after 'From'.");
        RuleFor(x => x.To)
            .Must((command, to) => to.DayNumber - command.From.DayNumber < MaxDays)
            .WithMessage($"A Mealie import covers at most {MaxDays} days.");
    }
}
