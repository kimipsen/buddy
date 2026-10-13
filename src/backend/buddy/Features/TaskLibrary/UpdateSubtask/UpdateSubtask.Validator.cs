using FluentValidation;

namespace buddy.Features.TaskLibrary;

public sealed class UpdateSubtaskValidator : AbstractValidator<UpdateSubtask>
{
    public UpdateSubtaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        // Zero for a checklist item, positive for a routine step -- that depends on the template's
        // kind, so the handler checks it (docs/backend/analysis/packing-checklists.md, Question 2).
        RuleFor(x => x.Duration).GreaterThanOrEqualTo(TimeSpan.Zero);
        RuleFor(x => x.Weekdays).Must(TaskLibraryWeekdays.IsValid).WithMessage(TaskLibraryWeekdays.InvalidMessage);
    }
}
