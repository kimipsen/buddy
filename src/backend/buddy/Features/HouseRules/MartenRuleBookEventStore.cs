using buddy.Common.Concurrency;

namespace buddy.Features.HouseRules;

public sealed class MartenRuleBookEventStore(IHouseRulesStore store) : IRuleBookEventStore
{
    public async Task<IReadOnlyCollection<RuleBookEvent>> ReadAsync(RuleBookId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => RuleBookEvent.FromPayload(e.Data))];
    }

    public async Task<RuleBook?> FindSnapshotAsync(RuleBookId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<RuleBookSnapshot>(id.Value, cancellationToken);

        return snapshot?.RuleBook;
    }

    public async Task CreateAsync(RuleBookId id, IReadOnlyCollection<RuleBookEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not RuleBookStarted)
        {
            throw new InvalidOperationException("The first event of a new rule book stream must be RuleBookStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(RuleBookId id, IReadOnlyCollection<RuleBookEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<RuleBookEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty rule book event."))];
}
