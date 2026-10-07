using buddy.Common.Concurrency;

using JasperFx;

using Marten;

namespace buddy.Features.Users;

public sealed class MartenUserEventStore(IUsersStore store) : IUserEventStore
{
    public async Task<KeycloakIdentity?> FindIdentityAsync(KeycloakSubject keycloakSubject, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.LoadAsync<KeycloakIdentity>(keycloakSubject.Value, cancellationToken);
    }

    public async Task DeleteAsync(UserId userId, KeycloakSubject keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken)
    {
        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty user event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(userId.Value, payloads);
        session.Store(new KeycloakIdentity(keycloakSubject.Value, userId, Deleted: true));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserEvent>> ReadAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(userId.Value, token: cancellationToken);
        session.ObserveStream(userId.Value, events);

        return [.. events.Select(e => UserEvent.FromPayload(e.Data))];
    }

    public async Task<User?> FindSnapshotAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<UserSnapshot>(userId.Value, cancellationToken);

        return snapshot?.User;
    }

    public async Task<IReadOnlyCollection<UserEventEntry>> ReadForwardAsync(UserId userId, long afterVersion, int take, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var events = await session.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == userId.Value && e.Version > afterVersion)
            .OrderBy(e => e.Version)
            .Take(take)
            .ToListAsync(cancellationToken);

        return [.. events.Select(e => new UserEventEntry(e.Version, UserEvent.FromPayload(e.Data)))];
    }

    public async Task<IReadOnlyCollection<UserEventEntry>> ReadBackwardAsync(UserId userId, long beforeVersion, int take, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var events = await session.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == userId.Value && e.Version < beforeVersion)
            .OrderByDescending(e => e.Version)
            .Take(take)
            .ToListAsync(cancellationToken);

        // Normalize back to ascending order so callers never need to know this was fetched in reverse.
        // `events` is IReadOnlyList<IEvent>, which has no in-place Reverse() -- Enumerable.Reverse()
        // is a non-mutating LINQ method, so its result must be used rather than discarded.
        return [.. events.Reverse().Select(e => new UserEventEntry(e.Version, UserEvent.FromPayload(e.Data)))];
    }

    public async Task<IReadOnlyCollection<UserEvent>> CreateAsync(KeycloakSubject keycloakSubject, UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken)
    {
        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty user event."))
            .ToArray();

        await using var session = store.LightweightSession();

        // Stored in the same transaction as the stream start: either both the identity
        // link and the events land, or neither does. Insert (not Store) rejects a
        // duplicate subject via the KeycloakIdentity primary key, which is what actually
        // guards against concurrent creation for the same subject -- across processes,
        // not just within one, unlike an in-memory gate.
        session.Insert(new KeycloakIdentity(keycloakSubject.Value, userId));
        session.StartTrackedStream(userId.Value, payloads);

        try
        {
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (DocumentAlreadyExistsException)
        {
            // Lost the race: another request already created this subject. Return what it produced.
            var winningUserId = (await FindIdentityAsync(keycloakSubject, cancellationToken))?.UserId
                ?? throw new InvalidOperationException($"Expected an existing Keycloak identity for subject '{keycloakSubject.Value}' after a creation conflict.");

            return await ReadAsync(winningUserId, cancellationToken);
        }

        return events;
    }

    public async Task AppendAsync(UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty user event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(userId.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }
}
