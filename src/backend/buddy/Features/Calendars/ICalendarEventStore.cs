using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public interface ICalendarEventStore
{
    Task<IReadOnlyCollection<CalendarEvent>> ReadAsync(CalendarId calendarId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarEvent>> CreateAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupOwnedCalendarDocument>> ListOwnedByGroupsAsync(IReadOnlyCollection<GroupId> groupIds, CancellationToken cancellationToken);
}
