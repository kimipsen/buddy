namespace buddy.Features.Calendars;

// Queryable read-model index kept alongside the Calendar event stream, the same pattern as
// KeycloakIdentity for Users. Filtered fields are raw Guid/enum rather than the strongly-typed
// CalendarId/UserId wrappers to keep Marten's Linq-to-SQL translation reliable. CalendarName is
// safe to cache here because nothing in this feature renames a calendar. Icon is NOT similarly
// fixed -- MartenCalendarEventStore re-stores every membership row for a calendar whenever
// CalendarIconChanged is appended (see its AppendAsync).
public sealed record CalendarMembershipDocument(string Id, Guid CalendarId, Guid UserId, CalendarRole Role, string CalendarName, string Icon)
{
    public static string BuildId(Guid calendarId, Guid userId) => $"{calendarId}:{userId}";
}
