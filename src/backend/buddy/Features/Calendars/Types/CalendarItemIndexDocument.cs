namespace buddy.Features.Calendars;

// Queryable read-model index kept alongside the CalendarItem event stream, so items belonging to
// a calendar can be listed without scanning every stream in the store.
public sealed record CalendarItemIndexDocument(Guid Id, Guid CalendarId, bool IsDeleted);
