namespace buddy.Features.Calendars;

// A computed instance of a CalendarItem's recurrence rule, resolved to an actual instant via the
// owning Calendar's TimeZoneId. Never persisted -- always recomputed from current item/calendar
// state (see CalendarOccurrenceExpansion), shared by ListOccurrences and the ical feed.
public sealed record CalendarItemOccurrence(
    CalendarItemId ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy);
