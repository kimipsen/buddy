namespace buddy.Features.Calendars;

// The structural rules a Recurrence must meet against the date it steps from (an event's start, a
// task's due date). Shared by CreateItemValidator, ScheduleTaskFromTemplateValidator and
// UpdateItemRecurrenceHandler (which only knows the seed once it has loaded the item). Keys match
// the request's own field paths.
public static class RecurrenceRules
{
    public const string IntervalCountKey = "Recurrence.IntervalCount";

    public const string UntilKey = "Recurrence.Until";

    public const string WeekdaysKey = "Recurrence.Weekdays";

    public static IEnumerable<(string Key, string Message)> Problems(Recurrence recurrence, DateOnly seed)
    {
        if (recurrence is not Recurrence.Repeating repeating)
        {
            yield break;
        }

        if (repeating.IntervalCount < 1)
        {
            yield return (IntervalCountKey, "Recurrence interval count must be at least 1.");
        }

        if (repeating.End is RecurrenceEnd.On on && on.Until < seed)
        {
            yield return (UntilKey, "Recurrence must not end before the item's first occurrence.");
        }

        foreach (var problem in WeekdayProblems(repeating, seed))
        {
            yield return problem;
        }
    }

    // See docs/backend/analysis/recurrence-weekdays.md for why a daily filter needs an interval of 1.
    private static IEnumerable<(string Key, string Message)> WeekdayProblems(Recurrence.Repeating repeating, DateOnly seed)
    {
        if (repeating.Weekdays == Weekdays.None)
        {
            yield break;
        }

        if ((repeating.Weekdays & ~Weekdays.All) != Weekdays.None)
        {
            yield return (WeekdaysKey, "Recurrence must repeat on at least one valid weekday.");
            yield break;
        }

        if (repeating.Frequency is not (RecurrenceFrequency.Daily or RecurrenceFrequency.Weekly))
        {
            yield return (WeekdaysKey, "Weekdays can only be chosen for a daily or weekly recurrence.");
            yield break;
        }

        if (repeating.Frequency == RecurrenceFrequency.Daily && repeating.IntervalCount != 1)
        {
            yield return (IntervalCountKey, "Recurrence interval count must be 1 when weekdays are chosen for a daily recurrence.");
            yield break;
        }

        // The first occurrence falls within one cycle of the seed (seven days, or IntervalCount
        // weeks from the seed's Monday), so only an end inside that cycle can leave none at all.
        // Bounding by the cycle also keeps the expansion short when UpdateItemRecurrenceValidator
        // passes DateOnly.MinValue as the seed. (An end before the seed is already reported above.)
        var cycleDays = repeating.Frequency == RecurrenceFrequency.Daily ? 7 : repeating.IntervalCount * 7;

        if (repeating.IntervalCount >= 1
            && repeating.End is RecurrenceEnd.On on
            && on.Until >= seed
            && on.Until.DayNumber - seed.DayNumber < cycleDays
            && RecurrenceExpansion.ExpandDates(seed, repeating, seed, on.Until).Count == 0)
        {
            yield return (WeekdaysKey, "Recurrence must occur on at least one of its weekdays before it ends.");
        }
    }
}
