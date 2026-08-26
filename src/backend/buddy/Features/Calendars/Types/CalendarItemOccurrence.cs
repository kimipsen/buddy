namespace buddy.Features.Calendars;

// A computed instance of a CalendarItem's recurrence rule, resolved to an actual instant via the
// owning Calendar's TimeZoneId. Never persisted -- always recomputed from current item/calendar
// state (see CalendarOccurrenceExpansion), shared by ListOccurrences and the ical feed. Icon is
// always the effective value (the item's own override, or the calendar's default when it has
// none) -- IconOverride carries the raw, possibly-null per-item value so an edit form can tell
// "inheriting" apart from "explicitly set to the same emoji as the default".
public sealed record CalendarItemOccurrence(
    CalendarItemId ItemId,
    CalendarItemKind Kind,
    string Title,
    string Icon,
    string? IconOverride,
    string Color,
    DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset? DueAt,
    bool IsAllDay,
    bool IsCompleted,
    Guid CreatedBy,
    Guid LastModifiedBy,
    Guid? AssignedTo);
