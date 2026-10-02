namespace buddy.Features.Calendars;

// Queryable read-model index for group-owned calendars, kept in the calendars schema (this
// document is about calendars, indexed by owning group) alongside CalendarMembershipDocument.
// Written at creation and re-stored (not deleted) on CalendarTransferredToGroup -- ownership can
// move between groups, see MartenCalendarEventStore.AppendAsync. CalendarName is safe to cache
// here for the same reason it is on CalendarMembershipDocument: nothing in this feature renames a
// calendar. Icon is NOT similarly fixed -- kept in sync on CalendarIconChanged the same way.
public sealed record GroupOwnedCalendarDocument(Guid Id, Guid GroupId, string CalendarName, string Icon);
