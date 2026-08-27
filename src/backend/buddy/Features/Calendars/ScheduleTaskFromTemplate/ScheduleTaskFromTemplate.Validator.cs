using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class ScheduleTaskFromTemplateValidator : AbstractValidator<ScheduleTaskFromTemplate>
{
    public ScheduleTaskFromTemplateValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);

        RuleFor(x => x.TaskTemplateId).NotEmpty();

        // Same rule as CreateItemValidator/UpdateItemRecurrenceValidator's own Recurrence check --
        // this command's Recurrence goes through the same RecurrenceUpdated-shaped value, so it's
        // held to the same structural constraint.
        RuleFor(x => x.Recurrence!.IntervalCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Recurrence interval count must be at least 1.")
            .When(x => x.Recurrence is not null);
    }
}
