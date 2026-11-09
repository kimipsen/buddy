using buddy.Common;

namespace buddy.Features.WorkLocations;

// A repeating cycle of CycleWeeks weeks, counted from AnchorMonday (cycle week 0); the cycle
// arithmetic is the shared WeekCycle, which NightPattern uses too.
//
// Days is a flat list rather than a nested dictionary so it serializes predictably inside a
// persisted event; days not listed have no location. Always kept sorted by (Week, Day) via
// Normalized(), so two patterns with the same content compare equal with IsSameAs.
public sealed record WorkPattern(int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<WorkPatternDay> Days)
{
    public const int MaxCycleWeeks = WeekCycle.MaxCycleWeeks;

    public static WorkPattern Empty(DateOnly today) => new(1, MondayOnOrBefore(today), []);

    public static DateOnly MondayOnOrBefore(DateOnly date) => WeekCycle.MondayOnOrBefore(date);

    public int CycleWeekOf(DateOnly date) => WeekCycle.CycleWeekOf(AnchorMonday, CycleWeeks, date);

    public WorkLocationId? LocationFor(DateOnly date)
    {
        var week = CycleWeekOf(date);
        var day = date.DayOfWeek;

        return Days.FirstOrDefault(d => d.Week == week && d.Day == day)?.LocationId;
    }

    public bool Uses(WorkLocationId locationId) => Days.Any(d => d.LocationId == locationId);

    public WorkPattern Without(WorkLocationId locationId) =>
        this with { Days = [.. Days.Where(d => d.LocationId != locationId)] };

    public WorkPattern Normalized() =>
        this with { Days = [.. Days.OrderBy(d => d.Week).ThenBy(d => WeekCycle.MondayFirstIndex(d.Day))] };

    // Records compare IReadOnlyList members by reference, so content equality is spelled out.
    public bool IsSameAs(WorkPattern other) =>
        CycleWeeks == other.CycleWeeks
        && AnchorMonday == other.AnchorMonday
        && Days.SequenceEqual(other.Days);
}

public sealed record WorkPatternDay(int Week, DayOfWeek Day, WorkLocationId LocationId);
