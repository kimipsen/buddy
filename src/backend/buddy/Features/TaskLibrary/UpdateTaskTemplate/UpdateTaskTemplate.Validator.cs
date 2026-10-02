using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class UpdateTaskTemplateValidator : AbstractValidator<UpdateTaskTemplate>
{
    public UpdateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        // A template always has its own icon (its subtasks may inherit it), so blank is rejected.
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.");
    }
}
