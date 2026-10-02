namespace buddy.Features.Calendars;

public static class RecurrenceExpansion
{
    // Every occurrence date for `recurrence` starting at `seed`, intersected with [from, to] (both
    // inclusive). A one-off item yields the seed date alone, if it falls in range. Each candidate is
    // computed as an offset from `seed` (never from the previous candidate), so a clamped monthly
    // occurrence (see AddMonthsClamped) never drags later occurrences off the seed's day-of-month.
    public static IReadOnlyCollection<DateOnly> ExpandDates(DateOnly seed, Recurrence recurrence, DateOnly from, DateOnly to) => recurrence switch
    {
        Recurrence.OneOff => seed >= from && seed <= to ? [seed] : [],
        Recurrence.Repeating repeating => ExpandRepeating(seed, repeating, from, to),
    };

    private static List<DateOnly> ExpandRepeating(DateOnly seed, Recurrence.Repeating rule, DateOnly from, DateOnly to)
    {
        var until = rule.End switch
        {
            RecurrenceEnd.On on when on.Until < to => on.Until,
            RecurrenceEnd.On or RecurrenceEnd.Never => to,
        };
        var dates = new List<DateOnly>();
        var step = 0;
        var current = seed;

        // Bounded by `until` directly (not generate-then-filter), so a seed far in the past with
        // a daily rule doesn't force stepping through years of dates before `from`.
        while (current <= until)
        {
            if (current >= from)
            {
                dates.Add(current);
            }

            step++;

            current = rule.Frequency switch
            {
                RecurrenceFrequency.Daily => seed.AddDays(step * rule.IntervalCount),
                RecurrenceFrequency.Weekly => seed.AddDays(step * rule.IntervalCount * 7),
                RecurrenceFrequency.Monthly => AddMonthsClamped(seed, step * rule.IntervalCount),
                RecurrenceFrequency.Yearly => AddMonthsClamped(seed, step * rule.IntervalCount * 12),
                _ => DateOnly.MaxValue
            };
        }

        return dates;
    }

    // DateOnly.AddMonths overflows a day that doesn't exist in the target month (e.g. Jan 31 +
    // 1 month -> Mar 3). Calendar apps instead clamp to the target month's last valid day
    // (Jan 31 -> Feb 28/29), which is what this does.
    private static DateOnly AddMonthsClamped(DateOnly seed, int months)
    {
        var totalMonths = seed.Year * 12 + (seed.Month - 1) + months;
        var year = totalMonths / 12;
        var month = totalMonths % 12 + 1;
        var day = Math.Min(seed.Day, DateTime.DaysInMonth(year, month));

        return new DateOnly(year, month, day);
    }
}
