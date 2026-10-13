using FluentValidation;

namespace buddy.Features.Feelings;

public sealed class LogFeelingCheckInValidator : AbstractValidator<LogFeelingCheckIn>
{
    public LogFeelingCheckInValidator()
    {
        this.ValidCheckInContent();

        RuleFor(x => x.Time)
            .Must((command, time) => command.Date.HasValue == time.HasValue)
            .WithMessage("Give both date and time, or neither for now.");
    }
}
