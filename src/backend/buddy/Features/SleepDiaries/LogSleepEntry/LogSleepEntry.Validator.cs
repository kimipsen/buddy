using FluentValidation;

namespace buddy.Features.SleepDiaries;

// Structural bounds only. Nothing here checks that the times are consistent with each other (fell
// asleep after bedtime, total matching the timestamps): every field is optional, a night crosses
// midnight, and the guardian's own estimates are authoritative (sleep-diary.md, edge-case table).
// No time-based gate on Date either -- a diary is filled in retroactively.
public sealed class LogSleepEntryValidator : AbstractValidator<LogSleepEntry>
{
    public const int MaxIntervals = 20;
    public const int MaxRemarksLength = 2000;

    private static readonly TimeSpan MaxIntervalDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan MaxTotalSleep = TimeSpan.FromHours(24);

    public LogSleepEntryValidator()
    {
        RuleFor(x => x.NightWakeUps).Must(list => list.Count <= MaxIntervals)
            .WithMessage($"A day can have at most {MaxIntervals} night wake-ups.");
        RuleForEach(x => x.NightWakeUps).ChildRules(IntervalRules);

        RuleFor(x => x.Naps).Must(list => list.Count <= MaxIntervals)
            .WithMessage($"A day can have at most {MaxIntervals} naps.");
        RuleForEach(x => x.Naps).ChildRules(IntervalRules);

        RuleFor(x => x.TotalSleepDuration)
            .Must(total => total is null || (total >= TimeSpan.Zero && total <= MaxTotalSleep))
            .WithMessage("totalSleepMinutes must be between 0 and 1440.");

        RuleFor(x => x.Remarks).MaximumLength(MaxRemarksLength);
    }

    private static void IntervalRules(InlineValidator<SleepInterval> interval) =>
        interval.RuleFor(i => i.Duration)
            .Must(d => d > TimeSpan.Zero && d <= MaxIntervalDuration)
            .WithMessage("durationMinutes must be between 1 and 720.");
}
