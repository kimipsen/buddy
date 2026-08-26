namespace buddy.Features.Calendars;

// IsAllDay defaults to false so events persisted before this property existed still deserialize.
// Time still carries a concrete value (by convention, local midnight) when IsAllDay is true --
// callers that render or export the due date should ignore the time-of-day in that case.
public sealed record DueDate(DateOnly Date, TimeOnly Time, bool IsAllDay = false);
