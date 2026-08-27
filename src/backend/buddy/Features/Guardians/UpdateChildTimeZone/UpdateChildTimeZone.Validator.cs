using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class UpdateChildTimeZoneValidator : AbstractValidator<UpdateChildTimeZone>
{
    public UpdateChildTimeZoneValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .Must(TimeZoneResolution.IsValid)
            .WithMessage(x => $"'{x.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");
    }
}
