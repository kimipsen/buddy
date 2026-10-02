using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class CreateTaskTemplateValidator : AbstractValidator<CreateTaskTemplate>
{
    public CreateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // A template always has its own icon (its subtasks may inherit it), so blank is rejected.
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.");
    }
}
