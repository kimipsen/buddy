namespace buddy.Features.Calendars;

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

// Whether a CalendarItem repeats. A one-off item happens on its seed date (the event's start, the
// task's due date) only; a repeating one steps from the seed every IntervalCount Frequency units
// until its End. Persisted in the events and the snapshot through RecurrenceJsonConverter. The
// wire keeps recurrence: null / until: null (RecurrenceRuleRequest). See
// docs/backend/analysis/eliminate-nulls.md, Phase 5.5.
public union Recurrence(Recurrence.OneOff, Recurrence.Repeating)
{
    public sealed record OneOff;

    public sealed record Repeating(RecurrenceFrequency Frequency, int IntervalCount, RecurrenceEnd End);
}

public union RecurrenceEnd(RecurrenceEnd.Never, RecurrenceEnd.On)
{
    public sealed record Never;

    // Inclusive: an occurrence on Until itself still happens.
    public sealed record On(DateOnly Until);
}
