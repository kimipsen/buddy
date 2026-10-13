using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class CopyTaskTemplateValidator : AbstractValidator<CopyTaskTemplate>
{
    public CopyTaskTemplateValidator()
    {
        RuleFor(x => x.ChildId.Value).NotEmpty().WithMessage("ChildId must not be empty.");
    }
}
