using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Guardians;

public sealed class UpdateChildHomeworkChunksValidator : AbstractValidator<UpdateChildHomeworkChunks>
{
    public UpdateChildHomeworkChunksValidator()
    {
        RuleFor(x => x.HomeworkChunks.FocusMinutes)
            .InclusiveBetween(HomeworkChunks.MinFocusMinutes, HomeworkChunks.MaxFocusMinutes)
            .OverridePropertyName("focusMinutes");

        RuleFor(x => x.HomeworkChunks.BreakMinutes)
            .InclusiveBetween(HomeworkChunks.MinBreakMinutes, HomeworkChunks.MaxBreakMinutes)
            .OverridePropertyName("breakMinutes");
    }
}
