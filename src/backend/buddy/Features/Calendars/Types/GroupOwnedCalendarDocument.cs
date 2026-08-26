namespace buddy.Features.Calendars;

// Queryable read-model index for group-owned calendars, kept in the calendars schema (this
// document is about calendars, indexed by owning group) alongside CalendarMembershipDocument.
// Written at creation and re-stored (not deleted) on CalendarTransferredToGroup -- ownership can
// move between groups, see MartenCalendarEventStore.AppendAsync. CalendarName is safe to cache
// here for the same reason it is on CalendarMembershipDocument: nothing in this feature renames a
// calendar. Icon is NOT similarly fixed -- kept in sync on CalendarIconChanged the same way, and
// is nullable for the same reason CalendarMembershipDocument.Icon is: rows written before this
// field existed have no "Icon" property in their stored JSON, so it deserializes as null rather
// than Calendar.DefaultIcon (see ListCalendarsEndpoint).
public sealed record GroupOwnedCalendarDocument(Guid Id, Guid GroupId, string CalendarName, string? Icon);
