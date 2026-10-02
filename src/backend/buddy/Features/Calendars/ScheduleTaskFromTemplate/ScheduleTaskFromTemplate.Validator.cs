using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class ScheduleTaskFromTemplateValidator : AbstractValidator<ScheduleTaskFromTemplate>
{
    public ScheduleTaskFromTemplateValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);

        RuleFor(x => x.TaskTemplateId).NotEmpty();

        // The same rules CreateItem and UpdateItemRecurrence apply; the seed is the start date.
        RuleFor(x => x.Recurrence).Custom((recurrence, context) =>
        {
            foreach (var (key, message) in RecurrenceRules.Problems(recurrence, context.InstanceToValidate.StartDate))
            {
                context.AddFailure(key, message);
            }
        });
    }
}
