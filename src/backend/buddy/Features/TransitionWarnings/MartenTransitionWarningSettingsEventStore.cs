using buddy.Common.Concurrency;

namespace buddy.Features.TransitionWarnings;

public sealed class MartenTransitionWarningSettingsEventStore(ITransitionWarningsStore store) : ITransitionWarningSettingsEventStore
{
    public async Task<IReadOnlyCollection<TransitionWarningEvent>> ReadAsync(TransitionWarningSettingsId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => TransitionWarningEvent.FromPayload(e.Data))];
    }

    public async Task<TransitionWarningSettings?> FindSnapshotAsync(TransitionWarningSettingsId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<TransitionWarningSettingsSnapshot>(id.Value, cancellationToken);

        return snapshot?.Settings;
    }

    public async Task CreateAsync(TransitionWarningSettingsId id, IReadOnlyCollection<TransitionWarningEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not TransitionWarningsStarted)
        {
            throw new InvalidOperationException("The first event of a new transition warning settings stream must be TransitionWarningsStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(TransitionWarningSettingsId id, IReadOnlyCollection<TransitionWarningEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, Payloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] Payloads(IReadOnlyCollection<TransitionWarningEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty transition warning event."))];
}
