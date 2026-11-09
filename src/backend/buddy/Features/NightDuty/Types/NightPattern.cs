using buddy.Common;

namespace buddy.Features.NightDuty;

// One version of a child's fixed night schedule: from EffectiveFrom on, a cycle of CycleWeeks weeks
// counted from AnchorMonday (the shared WeekCycle arithmetic WorkPattern uses), with one entry per
// (cycle week, weekday) that has one. A version applies until the next version's EffectiveFrom, so
// earlier nights keep the schedule they had (docs/backend/analysis/night-duty.md, Question 4).
//
// Days is a flat list rather than a nested dictionary so it serializes predictably inside a
// persisted event. Always kept sorted by (Week, Day) via Normalized(), so two versions with the
// same content compare equal with IsSameAs.
public sealed record NightPattern(DateOnly EffectiveFrom, int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<NightPatternDay> Days)
{
    public const int MaxCycleWeeks = WeekCycle.MaxCycleWeeks;

    public NightEntry? EntryFor(DateOnly date)
    {
        var week = WeekCycle.CycleWeekOf(AnchorMonday, CycleWeeks, date);
        var day = date.DayOfWeek;

        return Days.FirstOrDefault(d => d.Week == week && d.Day == day)?.Entry;
    }

    public NightPattern Normalized() =>
        this with { Days = [.. Days.OrderBy(d => d.Week).ThenBy(d => WeekCycle.MondayFirstIndex(d.Day))] };

    // Records compare IReadOnlyList members by reference, so content equality is spelled out.
    public bool IsSameAs(NightPattern other) =>
        EffectiveFrom == other.EffectiveFrom
        && CycleWeeks == other.CycleWeeks
        && AnchorMonday == other.AnchorMonday
        && Days.SequenceEqual(other.Days);
}

public sealed record NightPatternDay(int Week, DayOfWeek Day, NightEntry Entry);
