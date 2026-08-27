using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class CreateCalendarValidator : AbstractValidator<CreateCalendar>
{
    public CreateCalendarValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.TimeZoneId)
            .Must(TimeZoneResolution.IsValid)
            .WithMessage(x => $"'{x.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");

        RuleFor(x => x.Icon!.Value)
            .NotEmpty()
            .WithMessage("Icon must not be empty.")
            .When(x => x.Icon is not null);
    }
}
