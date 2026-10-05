using buddy.Common.Concurrency;

using Marten;

namespace buddy.Features.Babysitters;

public sealed class MartenBabysitterListEventStore(IBabysittersStore store) : IBabysitterListEventStore
{
    public async Task<IReadOnlyCollection<BabysitterEvent>> ReadAsync(BabysitterListId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => BabysitterEvent.FromPayload(e.Data))];
    }

    public async Task<BabysitterList?> FindSnapshotAsync(BabysitterListId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<BabysitterListSnapshot>(id.Value, cancellationToken);

        return snapshot?.BabysitterList;
    }

    public async Task<IReadOnlyCollection<BabysitterEvent>> CreateAsync(BabysitterListId id, IReadOnlyCollection<BabysitterEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not BabysitterListStarted)
        {
            throw new InvalidOperationException("The first event of a new babysitter list stream must be BabysitterListStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(BabysitterListId id, IReadOnlyCollection<BabysitterEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<BabysitterEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty babysitter event."))];
}
