using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class SetFeelingsDisplayModeValidator : AbstractValidator<SetFeelingsDisplayMode>
{
    public SetFeelingsDisplayModeValidator()
    {
        RuleFor(x => x.DisplayMode).IsInEnum();
    }
}
