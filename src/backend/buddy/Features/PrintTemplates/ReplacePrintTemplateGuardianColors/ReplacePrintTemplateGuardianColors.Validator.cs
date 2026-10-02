using FluentValidation;

namespace buddy.Features.PrintTemplates;

public sealed class ReplacePrintTemplateGuardianColorsValidator : AbstractValidator<ReplacePrintTemplateGuardianColors>
{
    public ReplacePrintTemplateGuardianColorsValidator()
    {
        RuleFor(x => x.Colors)
            .Must(colors => colors.Count <= PrintTemplateRules.MaxGuardianColors)
            .WithMessage($"A print template can color at most {PrintTemplateRules.MaxGuardianColors} guardians.")
            .Must(colors => colors.Select(c => c.GuardianId).Distinct().Count() == colors.Count)
            .WithMessage("Each guardian can have at most one color.")
            .OverridePropertyName("colors");

        RuleForEach(x => x.Colors)
            .Must(c => !string.IsNullOrWhiteSpace(c.Color.Value) && c.Color.Value.Length <= PrintTemplateRules.MaxColorLength)
            .WithMessage($"Each color must be 1–{PrintTemplateRules.MaxColorLength} characters.")
            .OverridePropertyName("colors");
    }
}
