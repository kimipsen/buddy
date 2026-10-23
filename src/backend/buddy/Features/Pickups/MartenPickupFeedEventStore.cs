using buddy.Common.Concurrency;

namespace buddy.Features.Pickups;

public sealed class MartenPickupFeedEventStore(IPickupsStore store) : IPickupFeedEventStore
{
    public async Task<IReadOnlyCollection<PickupFeedEvent>> ReadAsync(PickupFeedId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => PickupFeedEvent.FromPayload(e.Data))];
    }

    public async Task<PickupFeed?> FindSnapshotAsync(PickupFeedId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<PickupFeedSnapshot>(id.Value, cancellationToken);

        return snapshot?.PickupFeed;
    }

    public async Task CreateAsync(PickupFeedId id, IReadOnlyCollection<PickupFeedEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault().Value is not PickupFeedStarted)
        {
            throw new InvalidOperationException("The first event of a new pickup feed stream must be PickupFeedStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(PickupFeedId id, IReadOnlyCollection<PickupFeedEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] Payloads(IReadOnlyCollection<PickupFeedEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty pickup feed event."))];
}
