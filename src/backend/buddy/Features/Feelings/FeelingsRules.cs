using FluentValidation;

namespace buddy.Features.Feelings;

// The feature's limits, in one place (docs/backend/analysis/feelings-check-in.md, Questions 4-8).
public static class FeelingsRules
{
    public const int MaxTags = 3;
    public const int MaxNoteLength = 280;
    public const int MaxCheckInsPerDay = 20;
    public const int MaxPromptTimes = 4;
    // ListSleepDiaryEntriesHandler.MaxRangeDays: one snapshot read and a filter, nothing to expand.
    public const int MaxRangeDays = 92;
    public const int MaxShareLinkExpiryDays = 365;
    public const int SharedViewDefaultDays = 14;

    // Long enough to notice a mis-tap, short enough that a bad afternoon can't be erased at bedtime
    // (settled by the user 2026-10-13).
    public const int UndoWindowMinutes = 10;
    public static readonly TimeSpan UndoWindow = TimeSpan.FromMinutes(UndoWindowMinutes);

    // A guardian's device clock may run a little ahead of the server's.
    public static readonly TimeSpan FutureSlack = TimeSpan.FromMinutes(5);
}

// What CheckIn, LogFeelingCheckIn and EditFeelingCheckIn all carry.
public interface IFeelingCheckInContent
{
    FeelingZone Zone { get; }

    IReadOnlyList<FeelingTag> Tags { get; }

    string Note { get; }
}

public static class FeelingCheckInContentRules
{
    // Zone required; 0-3 distinct known tags, feeling words only from the chosen zone; a note of at
    // most 280 characters (already FreeText-normalized by the endpoint).
    public static void ValidCheckInContent<T>(this AbstractValidator<T> validator) where T : IFeelingCheckInContent
    {
        validator.RuleFor(x => x.Zone).IsInEnum();

        validator.RuleFor(x => x.Tags)
            .Must(tags => tags.Count <= FeelingsRules.MaxTags).WithMessage($"Choose at most {FeelingsRules.MaxTags} tags.")
            .Must(tags => tags.Distinct().Count() == tags.Count).WithMessage("Each tag can be chosen once.");

        validator.RuleForEach(x => x.Tags).IsInEnum();

        validator.RuleForEach(x => x.Tags)
            .Must((command, tag) => FeelingTags.FitsZone(tag, command.Zone))
            .WithMessage((command, tag) => $"'{tag}' is not a {command.Zone} feeling word.");

        validator.RuleFor(x => x.Note).MaximumLength(FeelingsRules.MaxNoteLength);
    }
}
