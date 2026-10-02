using FluentValidation;

namespace buddy.Features.Calendars;

public sealed class CreateItemValidator : AbstractValidator<CreateItem>
{
    public CreateItemValidator()
    {
        RuleFor(x => x.Title).MaximumLength(200);

        RuleFor(x => x.Recurrence!.IntervalCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Recurrence interval count must be at least 1.")
            .When(x => x.Recurrence is not null);

        // Which fields a kind carries is NewItemSchedule's job now. What's left is Period.TryCreate's
        // end-after-start check for an event, expressed here so it fires alongside every other
        // structural rule; CreateItemHandler calls TryCreate again only to obtain the Period value.
        RuleFor(x => x.Schedule).Custom((schedule, context) =>
        {
            if (schedule is NewItemSchedule.Event @event
                && Period.TryCreate(@event.StartsAt, @event.EndsAt, @event.IsAllDay) is PeriodValidationResult.Invalid(var message))
            {
                context.AddFailure("Schedule", message);
            }
        });
    }
}
