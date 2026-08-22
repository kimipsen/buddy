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

    // For a guardian's ListCalendars: a linked child's own calendars already have an Owner
    // CalendarMembershipDocument row (written at creation like any user-owned calendar), so this
    // reuses that document rather than introducing a new one -- unlike the group case, there's no
    // separate "GuardianOwnedCalendarDocument" needed.
    Task<IReadOnlyCollection<CalendarMembershipDocument>> ListOwnedByUsersAsync(IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken);
}
