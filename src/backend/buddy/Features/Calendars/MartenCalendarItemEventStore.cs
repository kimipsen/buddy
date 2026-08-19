using Marten;

namespace buddy.Features.Calendars;

public sealed class MartenCalendarItemEventStore(ICalendarsStore store) : ICalendarItemEventStore
{
    public async Task<IReadOnlyCollection<CalendarItemEvent>> ReadAsync(CalendarItemId itemId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(itemId.Value, token: cancellationToken);

        return [.. events.Select(e => CalendarItemEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<CalendarItemEvent>> CreateAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken)
    {
        var calendarId = events.FirstOrDefault() switch
        {
            EventItemCreated created => created.CalendarId,
            TaskItemCreated created => created.CalendarId,
            _ => throw new InvalidOperationException("The first event of a new calendar item stream must create the item."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty calendar item event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(itemId.Value, payloads);
        session.Store(new CalendarItemIndexDocument(itemId.Value, calendarId.Value, IsDeleted: false));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(CalendarItemId itemId, IReadOnlyCollection<CalendarItemEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty calendar item event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(itemId.Value, payloads);

        if (events.Any(e => e is ItemDeleted))
        {
            var doc = await session.LoadAsync<CalendarItemIndexDocument>(itemId.Value, cancellationToken);

            if (doc is not null)
            {
                session.Store(doc with { IsDeleted = true });
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CalendarItemId>> ListIdsForCalendarAsync(CalendarId calendarId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var docs = await session.Query<CalendarItemIndexDocument>()
            .Where(d => d.CalendarId == calendarId.Value && !d.IsDeleted)
            .ToListAsync(cancellationToken);

        return [.. docs.Select(d => new CalendarItemId(d.Id))];
    }
}
