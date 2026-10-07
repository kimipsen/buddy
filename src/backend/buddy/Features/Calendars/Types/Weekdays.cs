namespace buddy.Features.Calendars;

// The weekdays a Recurrence.Repeating occurs on. None is "no filter": a daily rule occurs every
// day and a weekly one on the seed's weekday. Any other value names the days explicitly; for a
// weekly rule, every N weeks counted in Monday-start weeks from the seed's week. A flags value
// rather than a set so the record keeps structural equality (UpdateItemRecurrenceHandler's no-op
// check relies on it). See docs/backend/analysis/recurrence-weekdays.md.
[Flags]
public enum Weekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}

public static class WeekdaysExtensions
{
    public static Weekdays ToWeekday(this DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => Weekdays.Monday,
        DayOfWeek.Tuesday => Weekdays.Tuesday,
        DayOfWeek.Wednesday => Weekdays.Wednesday,
        DayOfWeek.Thursday => Weekdays.Thursday,
        DayOfWeek.Friday => Weekdays.Friday,
        DayOfWeek.Saturday => Weekdays.Saturday,
        DayOfWeek.Sunday => Weekdays.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(day), day, "Not a day of the week."),
    };

    public static bool Includes(this Weekdays weekdays, DateOnly date) => (weekdays & date.DayOfWeek.ToWeekday()) != 0;

    // Monday first, matching how the week is shown in the UI.
    public static IReadOnlyList<DayOfWeek> ToDays(this Weekdays weekdays) =>
        [.. new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Where(day => (weekdays & day.ToWeekday()) != 0)];

    // The Monday of the (Monday-start) week `date` falls in.
    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}
