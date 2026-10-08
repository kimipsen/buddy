using System.Collections.Concurrent;

using buddy.Common.Concurrency;
using buddy.Features.Users;

namespace buddy.IntegrationTests.Common.Concurrency;

// Test-only IUserEventStore decorator (registered by BuddyApiFixture) that can simulate a second
// request landing between a handler's ReadAsync and its AppendAsync -- the race optimistic
// concurrency exists for, which real parallel requests can't hit deterministically. Inert unless
// a test arms a stream with InterleaveNextRead; it then fires once, on the first read of that
// stream inside a handler's tracking scope (reads outside one, such as the claims transformation,
// don't consume it), appending the armed event straight through Marten so the tracker never
// sees it.
public sealed class ConcurrentWriterUserEventStore(MartenUserEventStore inner, IUsersStore store) : IUserEventStore
{
    private static readonly ConcurrentDictionary<Guid, Func<UserEvent>> Armed = new();

    public static void InterleaveNextRead(Guid userId, Func<UserEvent> concurrentEvent) => Armed[userId] = concurrentEvent;

    public async Task<IReadOnlyCollection<UserEvent>> ReadAsync(UserId userId, CancellationToken cancellationToken)
    {
        var events = await inner.ReadAsync(userId, cancellationToken);

        if (StreamVersionTracker.IsTracking && Armed.TryRemove(userId.Value, out var concurrentEvent))
        {
            await using var session = store.LightweightSession();
            session.Events.Append(userId.Value, concurrentEvent().Value!);
            await session.SaveChangesAsync(cancellationToken);
        }

        return events;
    }

    public Task<KeycloakIdentity?> FindIdentityAsync(KeycloakSubject keycloakSubject, CancellationToken cancellationToken) =>
        inner.FindIdentityAsync(keycloakSubject, cancellationToken);

    public Task DeleteAsync(UserId userId, KeycloakSubject keycloakSubject, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken) =>
        inner.DeleteAsync(userId, keycloakSubject, events, cancellationToken);

    public Task EraseAsync(UserId userId, CancellationToken cancellationToken) => inner.EraseAsync(userId, cancellationToken);

    public Task RecordErasureAsync(UserId userId, CancellationToken cancellationToken) => inner.RecordErasureAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<UserId>> ListUnfinishedErasuresAsync(CancellationToken cancellationToken) =>
        inner.ListUnfinishedErasuresAsync(cancellationToken);

    public Task<User?> FindSnapshotAsync(UserId userId, CancellationToken cancellationToken) =>
        inner.FindSnapshotAsync(userId, cancellationToken);

    public Task<IReadOnlyCollection<UserEvent>> CreateAsync(KeycloakSubject keycloakSubject, UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken) =>
        inner.CreateAsync(keycloakSubject, userId, events, cancellationToken);

    public Task AppendAsync(UserId userId, IReadOnlyCollection<UserEvent> events, CancellationToken cancellationToken) =>
        inner.AppendAsync(userId, events, cancellationToken);
}
