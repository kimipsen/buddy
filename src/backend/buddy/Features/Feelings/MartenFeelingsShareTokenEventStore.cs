using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Feelings;

public sealed class MartenFeelingsShareTokenEventStore(IFeelingsStore store) : IFeelingsShareTokenEventStore
{
    public async Task<IReadOnlyCollection<FeelingsShareTokenEvent>> ReadAsync(FeelingsShareTokenId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => FeelingsShareTokenEvent.FromPayload(e.Data))];
    }

    public async Task<FeelingsShareToken?> FindSnapshotAsync(FeelingsShareTokenId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<FeelingsShareTokenSnapshot>(id.Value, cancellationToken);

        return snapshot?.FeelingsShareToken;
    }

    public async Task CreateAsync(FeelingsShareTokenId id, IReadOnlyCollection<FeelingsShareTokenEvent> events, CancellationToken cancellationToken)
    {
        var created = events.FirstOrDefault() switch
        {
            FeelingsShareTokenCreated e => e,
            _ => throw new InvalidOperationException("The first event of a new share token stream must be FeelingsShareTokenCreated."),
        };

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));
        session.Store(new FeelingsShareTokenDocument(
            id.Value,
            created.ChildId.Value,
            created.TokenHash,
            created.OccurredAt,
            created.ExpiresAt,
            IsRevoked: false));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(FeelingsShareTokenId id, IReadOnlyCollection<FeelingsShareTokenEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        // Flipped in the same transaction as the event, so a revoked link stops resolving the
        // moment RevokeFeelingsShareLink returns.
        if (events.Any(e => e.Value is FeelingsShareTokenRevoked)
            && await session.LoadAsync<FeelingsShareTokenDocument>(id.Value, cancellationToken) is { } document)
        {
            session.Store(document with { IsRevoked = true });
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeelingsShareTokenDocument?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<FeelingsShareTokenDocument>()
            .Where(d => d.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<FeelingsShareTokenDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var documents = await session.Query<FeelingsShareTokenDocument>()
            .Where(d => d.ChildId == childId.Value)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return documents;
    }

    private static object[] ToPayloads(IReadOnlyCollection<FeelingsShareTokenEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty feelings share token event."))];
}
