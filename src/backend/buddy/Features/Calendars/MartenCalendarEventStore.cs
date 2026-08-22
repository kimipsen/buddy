using buddy.Features.Groups;
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
        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty calendar event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(calendarId.Value, payloads);

        switch (events.FirstOrDefault())
        {
            case CalendarCreated created:
                session.Store(new CalendarMembershipDocument(
                    CalendarMembershipDocument.BuildId(calendarId.Value, created.OwnerId.Value),
                    calendarId.Value,
                    created.OwnerId.Value,
                    CalendarRole.Owner,
                    created.Name));
                break;

            case CalendarCreatedForGroup created:
                session.Store(new GroupOwnedCalendarDocument(calendarId.Value, created.OwnerId.Value, created.Name));
                break;

            default:
                throw new InvalidOperationException("The first event of a new calendar stream must be CalendarCreated or CalendarCreatedForGroup.");
        }

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
                    // this calendar already has it cached -- except a group-owned calendar has no
                    // membership document until its first explicit grant, so fall back to the
                    // GroupOwnedCalendarDocument written at creation for that case.
                    var name = await session.Query<CalendarMembershipDocument>()
                        .Where(d => d.CalendarId == calendarId.Value)
                        .Select(d => d.CalendarName)
                        .FirstOrDefaultAsync(cancellationToken)
                        ?? (await session.LoadAsync<GroupOwnedCalendarDocument>(calendarId.Value, cancellationToken))?.CalendarName
                        ?? throw new InvalidOperationException($"No membership or group-owned document found for calendar '{calendarId.Value}'.");

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

                    // A no-op for a user-owned calendar -- only group-owned calendars have one of these.
                    var groupOwned = await session.LoadAsync<GroupOwnedCalendarDocument>(calendarId.Value, cancellationToken);

                    if (groupOwned is not null)
                    {
                        session.Delete(groupOwned);
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

    public async Task<IReadOnlyCollection<GroupOwnedCalendarDocument>> ListOwnedByGroupsAsync(IReadOnlyCollection<GroupId> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        await using var session = store.QuerySession();
        var groupIdValues = groupIds.Select(g => g.Value).ToArray();

        return await session.Query<GroupOwnedCalendarDocument>()
            .Where(d => groupIdValues.Contains(d.GroupId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CalendarMembershipDocument>> ListOwnedByUsersAsync(IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        await using var session = store.QuerySession();
        var userIdValues = userIds.Select(u => u.Value).ToArray();

        return await session.Query<CalendarMembershipDocument>()
            .Where(d => userIdValues.Contains(d.UserId) && d.Role == CalendarRole.Owner)
            .ToListAsync(cancellationToken);
    }
}
