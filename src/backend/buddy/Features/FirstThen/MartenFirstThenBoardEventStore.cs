using buddy.Common.Concurrency;

namespace buddy.Features.FirstThen;

public sealed class MartenFirstThenBoardEventStore(IFirstThenStore store) : IFirstThenBoardEventStore
{
    public async Task<IReadOnlyCollection<FirstThenEvent>> ReadAsync(FirstThenBoardId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => FirstThenEvent.FromPayload(e.Data))];
    }

    public async Task<FirstThenBoard?> FindSnapshotAsync(FirstThenBoardId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<FirstThenBoardSnapshot>(id.Value, cancellationToken);

        return snapshot?.FirstThenBoard;
    }

    public async Task CreateAsync(FirstThenBoardId id, IReadOnlyCollection<FirstThenEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not FirstThenBoardStarted)
        {
            throw new InvalidOperationException("The first event of a new First-Then board stream must be FirstThenBoardStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(FirstThenBoardId id, IReadOnlyCollection<FirstThenEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<FirstThenEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty First-Then event."))];
}
