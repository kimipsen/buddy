using buddy.Features.Calendars;

namespace buddy.Features.Progress;

// One star-earning occurrence: a task item, the day it happened, and whether it was the whole task
// or one subtask -- the same identity as Calendars' CompletionKey plus the item it belongs to.
public sealed record OccurrenceKey(CalendarItemId ItemId, DateOnly OccurrenceDate, CompletionTarget Target);
