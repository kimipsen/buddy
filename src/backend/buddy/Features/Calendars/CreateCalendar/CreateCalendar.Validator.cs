using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class CreateCalendarValidator : AbstractValidator<CreateCalendar>
{
    public CreateCalendarValidator()
    {
        // The endpoint trims the name.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.TimeZoneId)
            .Must(TimeZoneResolution.IsValid)
            .WithMessage(x => $"'{x.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");

        // The icon is optional on create: CreateCalendarEndpoint maps a blank icon to
        // Calendar.DefaultIcon, so only a non-blank one reaches this rule.
        RuleFor(x => x.Icon.Value).ValidSizedIcon().OverridePropertyName("Icon");
    }
}
