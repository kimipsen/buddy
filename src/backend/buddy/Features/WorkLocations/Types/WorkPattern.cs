namespace buddy.Features.WorkLocations;

// A repeating cycle of CycleWeeks weeks, counted from AnchorMonday (cycle week 0). Anchored rather
// than based on ISO week parity because ISO parity drifts across 53-week years (2026 has a week
// 53, followed by an odd week 1) -- see docs/backend/analysis/work-locations.md, Question 4.
//
// Days is a flat list rather than a nested dictionary so it serializes predictably inside a
// persisted event; days not listed have no location. Always kept sorted by (Week, Day) via
// Normalized(), so two patterns with the same content compare equal with IsSameAs.
public sealed record WorkPattern(int CycleWeeks, DateOnly AnchorMonday, IReadOnlyList<WorkPatternDay> Days)
{
    public const int MaxCycleWeeks = 4;

    public static WorkPattern Empty(DateOnly today) => new(1, MondayOnOrBefore(today), []);

    public static DateOnly MondayOnOrBefore(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public int CycleWeekOf(DateOnly date)
    {
        var weeks = (MondayOnOrBefore(date).DayNumber - AnchorMonday.DayNumber) / 7;

        // Floored modulo, so dates before the anchor still land in the right cycle week.
        return ((weeks % CycleWeeks) + CycleWeeks) % CycleWeeks;
    }

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
        this with { Days = [.. Days.OrderBy(d => d.Week).ThenBy(d => ((int)d.Day + 6) % 7)] };

    // Records compare IReadOnlyList members by reference, so content equality is spelled out.
    public bool IsSameAs(WorkPattern other) =>
        CycleWeeks == other.CycleWeeks
        && AnchorMonday == other.AnchorMonday
        && Days.SequenceEqual(other.Days);
}

public sealed record WorkPatternDay(int Week, DayOfWeek Day, WorkLocationId LocationId);
