namespace buddy.Features.Calendars;

// Time still carries a concrete value (by convention, local midnight) when IsAllDay is true --
// callers that render or export the due date should ignore the time-of-day in that case.
public sealed record DueDate(DateOnly Date, TimeOnly Time, bool IsAllDay);
