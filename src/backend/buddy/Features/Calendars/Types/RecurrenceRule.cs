namespace buddy.Features.Calendars;

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public sealed record RecurrenceRule(RecurrenceFrequency Frequency, int IntervalCount, DateTimeOffset? Until = null);
