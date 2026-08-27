using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateCalendarIconValidator : AbstractValidator<UpdateCalendarIcon>
{
    public UpdateCalendarIconValidator()
    {
        RuleFor(x => x.Icon.Value).NotEmpty().WithMessage("Icon must not be empty.");
    }
}
