using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class UpdateItemRecurrenceValidator : AbstractValidator<UpdateItemRecurrence>
{
    public UpdateItemRecurrenceValidator()
    {
        // The seed date comes from the stored item, so the Until-vs-seed rule runs in the handler
        // once it is loaded; DateOnly.MinValue here leaves just the interval rule.
        RuleFor(x => x.Recurrence).Custom((recurrence, context) =>
        {
            foreach (var (key, message) in RecurrenceRules.Problems(recurrence, DateOnly.MinValue))
            {
                context.AddFailure(key, message);
            }
        });
    }
}
