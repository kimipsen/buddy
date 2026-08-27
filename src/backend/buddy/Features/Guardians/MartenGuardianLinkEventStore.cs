using buddy.Features.Users;

using Marten;

namespace buddy.Features.Guardians;

// Takes IUsersStore, not a dedicated store of its own -- GuardianLink's stream lives in the same
// Marten store/schema ("users") as User, so CreateChildAndLinkAsync can start both streams in one
// session/SaveChangesAsync. See docs/backend/analysis/child-accounts-and-guardian-roles.md.
public sealed class MartenGuardianLinkEventStore(IUsersStore store) : IGuardianLinkEventStore
{
    public async Task<IReadOnlyCollection<GuardianEvent>> ReadAsync(GuardianLinkId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => GuardianEvent.FromPayload(e.Data))];
    }

    public async Task AppendAsync(GuardianLinkId id, IReadOnlyCollection<GuardianEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        foreach (var @event in events)
        {
            switch (@event)
            {
                case GuardianRevoked:
                    await UpdateDocumentAsync(session, id, doc => doc with { IsRevoked = true }, cancellationToken);
                    break;

                case GuardianKindChanged changed:
                    await UpdateDocumentAsync(session, id, doc => doc with { Kind = changed.After }, cancellationToken);
                    break;
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<GuardianLinkDocument?> FindActiveLinkAsync(UserId childId, UserId guardianId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var document = await session.LoadAsync<GuardianLinkDocument>(GuardianLinkDocument.BuildId(childId.Value, guardianId.Value), cancellationToken);

        return document is { IsRevoked: false } ? document : null;
    }

    public async Task<IReadOnlyCollection<GuardianLinkDocument>> ListForGuardianAsync(UserId guardianId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GuardianLinkDocument>()
            .Where(d => d.GuardianId == guardianId.Value && !d.IsRevoked)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<GuardianLinkDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GuardianLinkDocument>()
            .Where(d => d.ChildId == childId.Value && !d.IsRevoked)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserId>> FilterChildrenAsync(IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        await using var session = store.QuerySession();
        var ids = userIds.Select(id => id.Value).ToArray();

        var childIds = await session.Query<GuardianLinkDocument>()
            .Where(d => ids.Contains(d.ChildId) && !d.IsRevoked)
            .Select(d => d.ChildId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. childIds.Select(id => new UserId(id))];
    }

    public async Task<(IReadOnlyCollection<UserEvent> UserEvents, IReadOnlyCollection<GuardianEvent> GuardianEvents)> CreateChildAndLinkAsync(
        KeycloakSubject childSubject,
        UserId childId,
        IReadOnlyCollection<UserEvent> userEvents,
        GuardianLinkId linkId,
        IReadOnlyCollection<GuardianEvent> guardianEvents,
        CancellationToken cancellationToken)
    {
        if (guardianEvents.FirstOrDefault() is not GuardianLinked linked)
        {
            throw new InvalidOperationException("The first event of a new guardian link stream must be GuardianLinked.");
        }

        var userPayloads = userEvents
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty user event."))
            .ToArray();

        var guardianPayloads = guardianEvents
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty guardian event."))
            .ToArray();

        await using var session = store.LightweightSession();

        // Same "both land or neither does" guarantee MartenUserEventStore.CreateAsync gives a plain
        // user signup, extended to also start the GuardianLink stream in the same transaction --
        // the fix for the doc's "Provisioning-time atomicity" gap.
        session.Insert(new KeycloakIdentity(childSubject.Value, childId));
        session.Events.StartStream(childId.Value, userPayloads);
        session.Events.StartStream(linkId.Value, guardianPayloads);
        session.Store(new GuardianLinkDocument(
            GuardianLinkDocument.BuildId(linked.ChildId.Value, linked.GuardianId.Value),
            linkId.Value,
            linked.ChildId.Value,
            linked.GuardianId.Value,
            linked.Kind,
            IsRevoked: false));

        await session.SaveChangesAsync(cancellationToken);

        return (userEvents, guardianEvents);
    }

    private static async Task UpdateDocumentAsync(IDocumentSession session, GuardianLinkId id, Func<GuardianLinkDocument, GuardianLinkDocument> update, CancellationToken cancellationToken)
    {
        var document = await session.Query<GuardianLinkDocument>()
            .Where(d => d.GuardianLinkId == id.Value)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"No GuardianLinkDocument found for guardian link '{id.Value}'.");

        session.Store(update(document));
    }
}
