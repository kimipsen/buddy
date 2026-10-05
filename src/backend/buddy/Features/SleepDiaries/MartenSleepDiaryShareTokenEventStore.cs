using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.SleepDiaries;

public sealed class MartenSleepDiaryShareTokenEventStore(ISleepDiariesStore store) : ISleepDiaryShareTokenEventStore
{
    public async Task<IReadOnlyCollection<SleepDiaryShareTokenEvent>> ReadAsync(SleepDiaryShareTokenId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => SleepDiaryShareTokenEvent.FromPayload(e.Data))];
    }

    public async Task<SleepDiaryShareToken?> FindSnapshotAsync(SleepDiaryShareTokenId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<SleepDiaryShareTokenSnapshot>(id.Value, cancellationToken);

        return snapshot?.SleepDiaryShareToken;
    }

    public async Task CreateAsync(SleepDiaryShareTokenId id, IReadOnlyCollection<SleepDiaryShareTokenEvent> events, CancellationToken cancellationToken)
    {
        var created = events.FirstOrDefault() switch
        {
            SleepDiaryShareTokenCreated e => e,
            _ => throw new InvalidOperationException("The first event of a new share token stream must be SleepDiaryShareTokenCreated."),
        };

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));
        session.Store(new SleepDiaryShareTokenDocument(
            id.Value,
            created.ChildId.Value,
            created.TokenHash,
            created.OccurredAt,
            created.ExpiresAt,
            IsRevoked: false));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(SleepDiaryShareTokenId id, IReadOnlyCollection<SleepDiaryShareTokenEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        // Flipped in the same transaction as the event, so a revoked link stops resolving the
        // moment RevokeSleepDiaryShareLink returns.
        if (events.Any(e => e.Value is SleepDiaryShareTokenRevoked)
            && await session.LoadAsync<SleepDiaryShareTokenDocument>(id.Value, cancellationToken) is { } document)
        {
            session.Store(document with { IsRevoked = true });
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<SleepDiaryShareTokenDocument?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<SleepDiaryShareTokenDocument>()
            .Where(d => d.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SleepDiaryShareTokenDocument>> ListForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var documents = await session.Query<SleepDiaryShareTokenDocument>()
            .Where(d => d.ChildId == childId.Value)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return documents;
    }

    private static object[] ToPayloads(IReadOnlyCollection<SleepDiaryShareTokenEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty sleep diary share token event."))];
}
