using buddy.Common.Concurrency;

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

        await session.SaveChangesAsync(cancellationToken);

        return events;
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
