using FluentValidation;

namespace buddy.Features.TaskLibrary;

// The approved suggestion becomes an item, so the title rules match AddSubtaskValidator.
public sealed class ApproveChecklistSuggestionValidator : AbstractValidator<ApproveChecklistSuggestion>
{
    public ApproveChecklistSuggestionValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Weekdays).Must(TaskLibraryWeekdays.IsValid).WithMessage(TaskLibraryWeekdays.InvalidMessage);
    }
}
