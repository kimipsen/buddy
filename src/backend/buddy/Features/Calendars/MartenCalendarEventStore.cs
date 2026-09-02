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

        // A CalendarIconChanged appended in the same initial batch (CreateCalendarHandler does
        // this when the caller specifies a custom icon at creation) overrides the default before
        // the very first document is written -- there's no window where a stale icon is cached.
        var icon = events.Select(e => e.Value).OfType<CalendarIconChanged>().FirstOrDefault()?.Icon.Value ?? Calendar.DefaultIcon.Value;

        switch (events.FirstOrDefault())
        {
            case CalendarCreated created:
                session.Store(new CalendarMembershipDocument(
                    CalendarMembershipDocument.BuildId(calendarId.Value, created.OwnerId.Value),
                    calendarId.Value,
                    created.OwnerId.Value,
                    CalendarRole.Owner,
                    created.Name,
                    icon));
                break;

            case CalendarCreatedForGroup created:
                session.Store(new GroupOwnedCalendarDocument(calendarId.Value, created.OwnerId.Value, created.Name, icon));
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
            await ApplyProjectionAsync(session, calendarId, @event, cancellationToken);
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    private static async Task ApplyProjectionAsync(IDocumentSession session, CalendarId calendarId, CalendarEvent @event, CancellationToken cancellationToken)
    {
        switch (@event)
        {
            case MemberRoleGranted granted:
                await ApplyMemberRoleGrantedAsync(session, calendarId, granted, cancellationToken);
                break;

            case MemberRoleRevoked revoked:
                session.Delete<CalendarMembershipDocument>(CalendarMembershipDocument.BuildId(calendarId.Value, revoked.MemberId.Value));
                break;

            case CalendarTransferredToGroup transferred:
                await ApplyCalendarTransferredToGroupAsync(session, calendarId, transferred, cancellationToken);
                break;

            case CalendarIconChanged changed:
                await ApplyCalendarIconChangedAsync(session, calendarId, changed, cancellationToken);
                break;

            case CalendarDeleted:
                await ApplyCalendarDeletedAsync(session, calendarId, cancellationToken);
                break;
        }
    }

    private static async Task ApplyMemberRoleGrantedAsync(IDocumentSession session, CalendarId calendarId, MemberRoleGranted granted, CancellationToken cancellationToken)
    {
        var (name, icon) = await ResolveCalendarNameAndIconAsync(session, calendarId, cancellationToken);

        session.Store(new CalendarMembershipDocument(
            CalendarMembershipDocument.BuildId(calendarId.Value, granted.MemberId.Value),
            calendarId.Value,
            granted.MemberId.Value,
            granted.Role,
            name,
            icon));
    }

    // Upserts the same row (Id = calendarId) with the new GroupId -- whether the calendar was
    // previously personal (no row yet) or owned by a different group (row already exists), this
    // is the only write needed either way.
    private static async Task ApplyCalendarTransferredToGroupAsync(IDocumentSession session, CalendarId calendarId, CalendarTransferredToGroup transferred, CancellationToken cancellationToken)
    {
        var (name, icon) = await ResolveCalendarNameAndIconAsync(session, calendarId, cancellationToken);
        session.Store(new GroupOwnedCalendarDocument(calendarId.Value, transferred.NewGroupId.Value, name, icon));
    }

    private static async Task ApplyCalendarIconChangedAsync(IDocumentSession session, CalendarId calendarId, CalendarIconChanged changed, CancellationToken cancellationToken)
    {
        var membershipRows = await session.Query<CalendarMembershipDocument>()
            .Where(d => d.CalendarId == calendarId.Value)
            .ToListAsync(cancellationToken);

        foreach (var row in membershipRows)
        {
            session.Store(row with { Icon = changed.Icon.Value });
        }

        var groupOwnedRow = await session.LoadAsync<GroupOwnedCalendarDocument>(calendarId.Value, cancellationToken);

        if (groupOwnedRow is not null)
        {
            session.Store(groupOwnedRow with { Icon = changed.Icon.Value });
        }
    }

    private static async Task ApplyCalendarDeletedAsync(IDocumentSession session, CalendarId calendarId, CancellationToken cancellationToken)
    {
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
    }

    // The calendar's name never changes, so any existing membership document for this calendar
    // already has it cached -- except a group-owned calendar has no membership document until
    // its first explicit grant, so fall back to the GroupOwnedCalendarDocument written at
    // creation (or a prior transfer) for that case. Icon is fetched from the same row for the
    // same reason (one lookup instead of two), even though -- unlike name -- it can change; the
    // row read here is always current since CalendarIconChanged updates it in the same AppendAsync
    // pass any icon-changing event would.
    private static async Task<(string Name, string Icon)> ResolveCalendarNameAndIconAsync(IDocumentSession session, CalendarId calendarId, CancellationToken cancellationToken)
    {
        var membership = await session.Query<CalendarMembershipDocument>()
            .Where(d => d.CalendarId == calendarId.Value)
            .Select(d => new { d.CalendarName, d.Icon })
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is not null)
        {
            return (membership.CalendarName, membership.Icon ?? Calendar.DefaultIcon.Value);
        }

        var groupOwned = await session.LoadAsync<GroupOwnedCalendarDocument>(calendarId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"No membership or group-owned document found for calendar '{calendarId.Value}'.");

        return (groupOwned.CalendarName, groupOwned.Icon ?? Calendar.DefaultIcon.Value);
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
