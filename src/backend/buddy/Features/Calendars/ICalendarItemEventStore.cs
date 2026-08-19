namespace buddy.Features.Calendars;

public interface ICalendarItemEventStore
{
    Task<IReadOnlyCollection<CalendarItemEvent>> ReadAsync(CalendarItemId itemId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarItemEvent>> CreateAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CalendarItemId>> ListIdsForCalendarAsync(CalendarId calendarId, CancellationToken cancellationToken);
}
