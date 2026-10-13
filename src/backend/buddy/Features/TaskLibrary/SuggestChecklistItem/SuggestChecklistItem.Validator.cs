using FluentValidation;

namespace buddy.Features.TaskLibrary;

// Title rules match AddSubtaskValidator: the suggestion may become an item.
public sealed class SuggestChecklistItemValidator : AbstractValidator<SuggestChecklistItem>
{
    public SuggestChecklistItemValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Weekdays).Must(TaskLibraryWeekdays.IsValid).WithMessage(TaskLibraryWeekdays.InvalidMessage);
    }
}
