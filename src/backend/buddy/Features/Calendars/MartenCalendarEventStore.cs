using buddy.Features.Users;

using Marten;

namespace buddy.Features.Calendars;

public sealed class MartenCalendarEventStore(ICalendarsStore store) : ICalendarEventStore
{
    public async Task<IReadOnlyCollection<CalendarEvent>> ReadAsync(CalendarId calendarId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(calendarId.Value, token: cancellationToken);

        return [.. events.Select(e => CalendarEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<CalendarEvent>> CreateAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not CalendarCreated created)
        {
            throw new InvalidOperationException("The first event of a new calendar stream must be CalendarCreated.");
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty calendar event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(calendarId.Value, payloads);
        session.Store(new CalendarMembershipDocument(
            CalendarMembershipDocument.BuildId(calendarId.Value, created.OwnerId.Value),
            calendarId.Value,
            created.OwnerId.Value,
            CalendarRole.Owner,
            created.Name));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(CalendarId calendarId, IReadOnlyCollection<CalendarEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty calendar event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(calendarId.Value, payloads);

        foreach (var @event in events)
        {
            switch (@event)
            {
                case MemberRoleGranted granted:
                    // The calendar's name never changes, so any existing membership document for
                    // this calendar (there's always at least the owner's) already has it cached.
                    var name = await session.Query<CalendarMembershipDocument>()
                        .Where(d => d.CalendarId == calendarId.Value)
                        .Select(d => d.CalendarName)
                        .FirstAsync(cancellationToken);

                    session.Store(new CalendarMembershipDocument(
                        CalendarMembershipDocument.BuildId(calendarId.Value, granted.MemberId.Value),
                        calendarId.Value,
                        granted.MemberId.Value,
                        granted.Role,
                        name));
                    break;

                case MemberRoleRevoked revoked:
                    session.Delete<CalendarMembershipDocument>(CalendarMembershipDocument.BuildId(calendarId.Value, revoked.MemberId.Value));
                    break;

                case CalendarDeleted:
                    var members = await session.Query<CalendarMembershipDocument>()
                        .Where(d => d.CalendarId == calendarId.Value)
                        .ToListAsync(cancellationToken);

                    foreach (var member in members)
                    {
                        session.Delete(member);
                    }
                    break;
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CalendarMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<CalendarMembershipDocument>()
            .Where(d => d.UserId == userId.Value)
            .ToListAsync(cancellationToken);
    }
}
