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

        // No Icon rule: the icon is optional on create, and CreateCalendarEndpoint maps a blank icon
        // to Calendar.DefaultIcon, so a blank Icon can't reach this validator. UpdateCalendarIconValidator does reject
        // a blank icon, because there the icon is required.
    }
}
