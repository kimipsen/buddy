using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class CreateTaskTemplateValidator : AbstractValidator<CreateTaskTemplate>
{
    public CreateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // A template always has its own icon (its subtasks may inherit it), so blank is rejected.
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.");

        RuleFor(x => x.Kind).IsInEnum();

        // A display option of packing lists only (docs/backend/analysis/packing-checklists.md).
        RuleFor(x => x.EveningBeforeFrom)
            .Null()
            .When(x => x.Kind != TaskTemplateKind.Checklist)
            .WithMessage("Only a packing list can be shown the evening before.");
    }
}
