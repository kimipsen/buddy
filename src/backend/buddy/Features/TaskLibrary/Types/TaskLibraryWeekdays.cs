using buddy.Features.Calendars;

namespace buddy.Features.TaskLibrary;

// The wire form of an item's or a suggestion's weekdays: null, or all seven days, means "every day
// the list occurs" (Weekdays.None); otherwise the listed days. Parsed like
// RecurrenceRuleRequest.Weekdays: an empty list or an undefined day number maps to a bit outside
// Weekdays.All, which the validators reject as a 400 rather than reading it as "no filter".
public static class TaskLibraryWeekdays
{
    public const Weekdays Invalid = (Weekdays)(1 << 7);

    public const string InvalidMessage = "Weekdays must list at least one valid day.";

    public static Weekdays FromDays(IReadOnlyList<DayOfWeek>? days)
    {
        if (days is null)
        {
            return Weekdays.None;
        }

        var weekdays = days.Count == 0
            ? Invalid
            : days.Aggregate(Weekdays.None, (set, day) => set | (Enum.IsDefined(day) ? day.ToWeekday() : Invalid));

        return weekdays == Weekdays.All ? Weekdays.None : weekdays;
    }

    public static bool IsValid(Weekdays weekdays) => (weekdays & ~Weekdays.All) == Weekdays.None;
}
