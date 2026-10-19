using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class CreateTaskTemplateValidator : AbstractValidator<CreateTaskTemplate>
{
    // Same cap as a work location's color (WorkLocationRules.MaxColorLength).
    public const int MaxColorLength = 32;

    public CreateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // Optional (blank is allowed), but capped.
        RuleFor(x => x.Color.Value).MaximumLength(MaxColorLength).OverridePropertyName("Color");

        // A template always has its own icon (its subtasks may inherit it), so blank is rejected.
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.").OverridePropertyName("Icon");
        RuleFor(x => x.Icon.Value).ValidIcon().OverridePropertyName("Icon");

        RuleFor(x => x.Kind).IsInEnum();

        // A display option of packing lists only (docs/backend/analysis/packing-checklists.md).
        RuleFor(x => x.EveningBeforeFrom)
            .Null()
            .When(x => x.Kind != TaskTemplateKind.Checklist)
            .WithMessage("Only a packing list can be shown the evening before.");
    }
}
