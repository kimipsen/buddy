using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenAiSessionEventStore(IMealplansStore store) : IAiSessionEventStore
{
    public async Task<IReadOnlyCollection<MealplanAiSessionEvent>> ReadAsync(MealplanAiSessionId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => MealplanAiSessionEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<MealplanAiSessionEvent>> CreateAsync(MealplanAiSessionId id, IReadOnlyCollection<MealplanAiSessionEvent> events, CancellationToken cancellationToken)
    {
        var started = events.FirstOrDefault() switch
        {
            AiSessionStarted e => e,
            _ => throw new InvalidOperationException("The first event of a new AI session stream must be AiSessionStarted."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty AI session event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new AiSessionIndexDocument(id.Value, started.ChildId.Value, started.OccurredAt));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MealplanAiSessionId id, IReadOnlyCollection<MealplanAiSessionEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty AI session event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<(MealplanAiSessionId Id, DateTimeOffset StartedAt)?> FindLatestForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<AiSessionIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .OrderByDescending(d => d.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : (new MealplanAiSessionId(doc.Id), doc.StartedAt);
    }
}
