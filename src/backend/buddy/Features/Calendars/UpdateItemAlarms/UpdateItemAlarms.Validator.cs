using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateItemAlarmsValidator : AbstractValidator<UpdateItemAlarms>
{
    public UpdateItemAlarmsValidator()
    {
        RuleFor(x => x.Alarms).ValidAlarms();
    }
}
