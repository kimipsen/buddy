namespace buddy.Features.Calendars;

// Queryable read-model index for group-owned calendars, kept in the calendars schema (this
// document is about calendars, indexed by owning group) alongside CalendarMembershipDocument.
// Written once at creation -- ownership is fixed, so this never needs updating afterward, only
// deletion alongside CalendarDeleted/a group's cascade delete. CalendarName is safe to cache here
// for the same reason it is on CalendarMembershipDocument: nothing in this feature renames a calendar.
public sealed record GroupOwnedCalendarDocument(Guid Id, Guid GroupId, string CalendarName);
