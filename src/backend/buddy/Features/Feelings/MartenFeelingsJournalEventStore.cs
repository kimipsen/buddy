using buddy.Common.Concurrency;

namespace buddy.Features.Feelings;

public sealed class MartenFeelingsJournalEventStore(IFeelingsStore store) : IFeelingsJournalEventStore
{
    public async Task<IReadOnlyCollection<FeelingsJournalEvent>> ReadAsync(FeelingsJournalId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => FeelingsJournalEvent.FromPayload(e.Data))];
    }

    public async Task<FeelingsJournal?> FindSnapshotAsync(FeelingsJournalId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<FeelingsJournalSnapshot>(id.Value, cancellationToken);

        return snapshot?.FeelingsJournal;
    }

    public async Task CreateAsync(FeelingsJournalId id, IReadOnlyCollection<FeelingsJournalEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not FeelingsJournalStarted)
        {
            throw new InvalidOperationException("The first event of a new feelings journal stream must be FeelingsJournalStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(FeelingsJournalId id, IReadOnlyCollection<FeelingsJournalEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<FeelingsJournalEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty feelings journal event."))];
}
