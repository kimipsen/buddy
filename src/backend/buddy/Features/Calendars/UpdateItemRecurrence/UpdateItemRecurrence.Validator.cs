using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateItemRecurrenceValidator : AbstractValidator<UpdateItemRecurrence>
{
    public UpdateItemRecurrenceValidator()
    {
        RuleFor(x => x.Recurrence!.IntervalCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Recurrence interval count must be at least 1.")
            .When(x => x.Recurrence is not null);
    }
}
