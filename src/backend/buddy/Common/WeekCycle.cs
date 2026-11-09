namespace buddy.Common;

// Weekday cycles of 1–4 weeks counted from an anchor Monday (cycle week 0), shared by work
// locations (WorkPattern) and night duty (NightPattern). Anchored rather than based on ISO week
// parity because ISO parity drifts across 53-week years (2026 has a week 53, followed by an odd
// week 1) -- see docs/backend/analysis/work-locations.md, Question 4.
public static class WeekCycle
{
    public const int MaxCycleWeeks = 4;

    public static DateOnly MondayOnOrBefore(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    public static int CycleWeekOf(DateOnly anchorMonday, int cycleWeeks, DateOnly date)
    {
        var weeks = (MondayOnOrBefore(date).DayNumber - anchorMonday.DayNumber) / 7;

        // Floored modulo, so dates before the anchor still land in the right cycle week.
        return ((weeks % cycleWeeks) + cycleWeeks) % cycleWeeks;
    }

    // Monday-first ordering for (Week, Day) sorting: Monday = 0 ... Sunday = 6.
    public static int MondayFirstIndex(DayOfWeek day) => ((int)day + 6) % 7;
}
