using buddy.Features.Calendars;

using FluentValidation;

namespace buddy.Features.Users;

public sealed class UpdateTimeZoneValidator : AbstractValidator<UpdateTimeZone>
{
    public UpdateTimeZoneValidator()
    {
        RuleFor(x => x.TimeZoneId)
            .Must(TimeZoneResolution.IsValid)
            .WithMessage(x => $"'{x.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");
    }
}
