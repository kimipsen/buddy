using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public interface ICalendarEventStore
{
    // Masks the calendar's name and the titles of all its items (CalendarsPersonalData's rules) and
    // rebuilds their snapshots. For a deleted calendar of a group erased with its last member
    // (GroupsPersonalDataEraser); idempotent. See gdpr-data-protection.md.
    Task EraseAsync(CalendarId calendarId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarEvent>> ReadAsync(CalendarId calendarId, CancellationToken cancellationToken);

    Task<Calendar?> FindSnapshotAsync(CalendarId calendarId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarEvent>> CreateAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupOwnedCalendarDocument>> ListOwnedByGroupsAsync(IReadOnlyCollection<GroupId> groupIds, CancellationToken cancellationToken);
}
