using System.Diagnostics;

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

        RuleFor(x => x.AssignedTo)
            .Null()
            .WithMessage("Only a task can be assigned to someone.")
            .When(x => x.Kind == CalendarItemKind.Event);

        RuleFor(x => x.StartsAt)
            .NotNull()
            .WithMessage("An event requires both a start and an end time.")
            .When(x => x.Kind == CalendarItemKind.Event);

        RuleFor(x => x.EndsAt)
            .NotNull()
            .WithMessage("An event requires both a start and an end time.")
            .When(x => x.Kind == CalendarItemKind.Event);

        // Period.TryCreate's own end-after-start check, re-expressed here so it fires alongside
        // every other structural CreateItem rule instead of via a separate handler-side branch --
        // CreateItemHandler still calls Period.TryCreate itself to obtain the Period value.
        RuleFor(x => x)
            .Must(x => Period.TryCreate(x.StartsAt!, x.EndsAt!, x.IsAllDay) is PeriodValidationResult.Valid)
            .WithMessage(x => Period.TryCreate(x.StartsAt!, x.EndsAt!, x.IsAllDay) switch
            {
                PeriodValidationResult.Invalid(var message) => message,
                PeriodValidationResult.Valid => throw new UnreachableException("Already excluded by the enclosing Must check."),
            })
            .When(x => x.Kind == CalendarItemKind.Event && x.StartsAt is not null && x.EndsAt is not null);

        RuleFor(x => x.DueDate)
            .NotNull()
            .WithMessage("A task requires a due date.")
            .When(x => x.Kind == CalendarItemKind.Task);
    }
}
