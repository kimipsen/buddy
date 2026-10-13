using FluentValidation;

namespace buddy.Features.Feelings;

// 0-4 distinct whole-minute times on the child's wall clock (Question 6). An empty list turns the
// prompts off.
public sealed class SetCheckInPromptTimesValidator : AbstractValidator<SetCheckInPromptTimes>
{
    public SetCheckInPromptTimesValidator()
    {
        RuleFor(x => x.Times)
            .Must(times => times.Count <= FeelingsRules.MaxPromptTimes).WithMessage($"At most {FeelingsRules.MaxPromptTimes} prompt times.")
            .Must(times => times.Distinct().Count() == times.Count).WithMessage("Prompt times must be different from each other.")
            .Must(times => times.All(t => t.Second == 0 && t.Millisecond == 0 && t.Microsecond == 0)).WithMessage("Prompt times are whole minutes.");
    }
}
