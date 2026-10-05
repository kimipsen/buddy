using FluentValidation;

namespace buddy.Features.SleepDiaries;

public sealed class UpdateSleepHygieneNotesValidator : AbstractValidator<UpdateSleepHygieneNotes>
{
    public const int MaxNotesLength = 4000;

    public UpdateSleepHygieneNotesValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(MaxNotesLength);
    }
}
