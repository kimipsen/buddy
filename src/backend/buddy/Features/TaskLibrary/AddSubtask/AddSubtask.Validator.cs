using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class AddSubtaskValidator : AbstractValidator<AddSubtask>
{
    public AddSubtaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Duration).GreaterThan(TimeSpan.Zero);
    }
}
