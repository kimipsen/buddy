using buddy.Common.Concurrency;

using JasperFx.Events;

using Marten;
using Marten.Events;

namespace buddy.Features.Progress;

public sealed class MartenProgressEventStore(IProgressStore store) : IProgressEventStore
{
    public async Task<IReadOnlyCollection<ProgressEvent>> ReadAsync(ProgressId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => ProgressEvent.FromPayload(e.Data))];
    }

    public async Task<ChildProgress?> FindSnapshotAsync(ProgressId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<ChildProgressSnapshot>(id.Value, cancellationToken);

        return snapshot?.ChildProgress;
    }

    public async Task<IReadOnlyCollection<ProgressEvent>> CreateAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not ProgressStarted)
        {
            throw new InvalidOperationException("The first event of a new progress stream must be ProgressStarted.");
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty progress event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);

        await session.SaveStartedStreamAsync("progress", cancellationToken);

        return events;
    }

    public async Task<FlashCardStarsAwarded?> FindFlashCardAwardAsync(ProgressId id, Guid gameId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == id.Value && e.EventTypesAre(typeof(FlashCardStarsAwarded)))
            .ToListAsync(cancellationToken);

        return events
            .Select(e => e.Data)
            .OfType<FlashCardStarsAwarded>()
            .FirstOrDefault(e => e.GameId == gameId);
    }

    public async Task AppendAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty progress event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }
}
