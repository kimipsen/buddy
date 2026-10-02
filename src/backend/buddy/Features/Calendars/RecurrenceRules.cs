namespace buddy.Features.Calendars;

// The structural rules a Recurrence must meet against the date it steps from (an event's start, a
// task's due date). Shared by CreateItemValidator, ScheduleTaskFromTemplateValidator and
// UpdateItemRecurrenceHandler (which only knows the seed once it has loaded the item). Keys match
// the request's own field paths.
public static class RecurrenceRules
{
    public const string IntervalCountKey = "Recurrence.IntervalCount";

    public const string UntilKey = "Recurrence.Until";

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
    }
}
