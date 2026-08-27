using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class UpdateSubtaskValidator : AbstractValidator<UpdateSubtask>
{
    public UpdateSubtaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Duration).GreaterThan(TimeSpan.Zero);
    }
}
