using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class ScheduleTaskFromTemplateValidator : AbstractValidator<ScheduleTaskFromTemplate>
{
    public ScheduleTaskFromTemplateValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);

        RuleFor(x => x.Icon!.Value).ValidIcon().OverridePropertyName("Icon").When(x => x.Icon is not null);

        // Optional; the endpoint trims it.
        RuleFor(x => x.Color.Value).MaximumLength(Color.MaxLength).OverridePropertyName("Color");

        RuleFor(x => x.TaskTemplateId).NotEmpty();

        RuleFor(x => x.Alarms).ValidAlarms();

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
