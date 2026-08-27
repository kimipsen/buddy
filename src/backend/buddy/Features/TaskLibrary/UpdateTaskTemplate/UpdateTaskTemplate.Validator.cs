using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class UpdateTaskTemplateValidator : AbstractValidator<UpdateTaskTemplate>
{
    public UpdateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
