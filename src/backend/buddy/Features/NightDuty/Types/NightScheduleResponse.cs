using buddy.Features.Babysitters;

namespace buddy.Features.NightDuty;

// Whether a fixed-schedule version applies to the child's today (Current), only to earlier nights
// (Past) or only from a later date (Upcoming).
public enum NightPatternStatus
{
    Past,
    Current,
    Upcoming
}

// The editor's view of a child's fixed schedule: every version, oldest first. Overrides are
// deliberately not included -- they're read resolved, through ListNights.
public sealed record NightScheduleResponse(Guid ChildId, DateOnly Today, IReadOnlyList<NightPatternResponse> Patterns)
{
    public static NightScheduleResponse From(NightSchedule schedule, DateOnly today, BabysitterNames babysitterNames)
    {
        var current = schedule.PatternFor(today);

        return new(
            schedule.ChildId.Value,
            today,
            [.. schedule.Patterns.Select(p => NightPatternResponse.From(
                p,
                StatusOf(p, current, today),
                babysitterNames))]);
    }

    private static NightPatternStatus StatusOf(NightPattern pattern, NightPattern? current, DateOnly today)
    {
        if (pattern.EffectiveFrom > today)
        {
            return NightPatternStatus.Upcoming;
        }

        return pattern == current ? NightPatternStatus.Current : NightPatternStatus.Past;
    }
}

public sealed record NightPatternResponse(
    DateOnly EffectiveFrom, int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<NightPatternDayDto> Days, NightPatternStatus Status)
{
    public static NightPatternResponse From(NightPattern pattern, NightPatternStatus status, BabysitterNames babysitterNames) =>
        new(
            pattern.EffectiveFrom,
            pattern.CycleWeeks,
            pattern.AnchorMonday,
            [.. pattern.Days.Select(d => new NightPatternDayDto(d.Week, d.Day, NightEntryDto.FromDomain(d.Entry, babysitterNames)))],
            status);
}

public sealed record NightPatternDayDto(int Week, DayOfWeek Day, NightEntryDto Entry);
