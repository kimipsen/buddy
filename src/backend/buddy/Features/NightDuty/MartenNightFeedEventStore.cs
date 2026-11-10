using buddy.Common.Concurrency;

namespace buddy.Features.NightDuty;

public sealed class MartenNightFeedEventStore(INightDutyStore store) : INightFeedEventStore
{
    public async Task<IReadOnlyCollection<NightFeedEvent>> ReadAsync(NightFeedId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => NightFeedEvent.FromPayload(e.Data))];
    }

    public async Task<NightFeed?> FindSnapshotAsync(NightFeedId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<NightFeedSnapshot>(id.Value, cancellationToken);

        return snapshot?.NightFeed;
    }

    public async Task CreateAsync(NightFeedId id, IReadOnlyCollection<NightFeedEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault().Value is not NightFeedStarted)
        {
            throw new InvalidOperationException("The first event of a new night feed stream must be NightFeedStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(NightFeedId id, IReadOnlyCollection<NightFeedEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] Payloads(IReadOnlyCollection<NightFeedEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty night feed event."))];
}
