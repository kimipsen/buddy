using FluentValidation;

namespace buddy.Features.PrintTemplates;

public sealed class ReplacePrintTemplateBabysitterColorsValidator : AbstractValidator<ReplacePrintTemplateBabysitterColors>
{
    public ReplacePrintTemplateBabysitterColorsValidator()
    {
        RuleFor(x => x.Colors)
            .Must(colors => colors.Count <= PrintTemplateRules.MaxBabysitterColors)
            .WithMessage($"A print template can color at most {PrintTemplateRules.MaxBabysitterColors} babysitters.")
            .Must(colors => colors.Select(c => (c.GuardianId, c.BabysitterId)).Distinct().Count() == colors.Count)
            .WithMessage("Each babysitter can have at most one color.")
            .OverridePropertyName("colors");

        RuleForEach(x => x.Colors)
            .Must(c => !string.IsNullOrWhiteSpace(c.Color.Value) && c.Color.Value.Length <= PrintTemplateRules.MaxColorLength)
            .WithMessage($"Each color must be 1–{PrintTemplateRules.MaxColorLength} characters.")
            .OverridePropertyName("colors");
    }
}
