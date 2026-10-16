using FluentValidation;

namespace buddy.Features.VisitSummaries;

public static class VisitSummaryRules
{
    // The sleep diary's, feelings' and effect notes' range: the report reads all three.
    public const int MaxRangeDays = 92;

    public const int MaxTitleLength = 100;

    public const int MaxNoteLength = 4000;

    // One visit needs one or two links (the clinic, a second opinion); the cap keeps a forgotten
    // summary from collecting them.
    public const int MaxLiveLinks = 5;

    // As the sleep diary's links: meant for a consultation, not a standing feed.
    public const int MaxShareLinkExpiryDays = 365;

    // Shared by CreateVisitSummary and UpdateVisitSummary, which both take a whole VisitSummaryDetails.
    public static void ValidDetails<T>(this AbstractValidator<T> validator, Func<T, VisitSummaryDetails> details)
    {
        validator.RuleFor(x => details(x).Title)
            .NotEmpty().WithMessage("title is required.")
            .MaximumLength(MaxTitleLength).WithMessage($"title cannot be longer than {MaxTitleLength} characters.")
            .OverridePropertyName("title");

        validator.RuleFor(x => details(x).Note)
            .MaximumLength(MaxNoteLength).WithMessage($"note cannot be longer than {MaxNoteLength} characters.")
            .OverridePropertyName("note");

        validator.RuleFor(x => details(x).Sections)
            .NotEmpty().WithMessage("Choose at least one section.")
            .Must(sections => sections.All(Enum.IsDefined)).WithMessage("sections contains an unknown section.")
            .OverridePropertyName("sections");

        validator.RuleFor(x => details(x).To)
            .Must((x, to) => to >= details(x).From).WithMessage("'to' must not be before 'from'.")
            .Must((x, to) => to.DayNumber - details(x).From.DayNumber < MaxRangeDays)
            .WithMessage($"The period cannot be longer than {MaxRangeDays} days.")
            .OverridePropertyName("to");
    }
}
