using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class CreateTaskTemplateValidator : AbstractValidator<CreateTaskTemplate>
{
    public CreateTaskTemplateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}
