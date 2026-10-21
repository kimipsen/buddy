using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenMealieConnectionEventStore(IMealplansStore store) : IMealieConnectionEventStore
{
    public async Task<IReadOnlyCollection<MealieConnectionEvent>> ReadAsync(MealieConnectionId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => MealieConnectionEvent.FromPayload(e.Data))];
    }

    public async Task<MealieConnection?> FindSnapshotAsync(MealieConnectionId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<MealieConnectionSnapshot>(id.Value, cancellationToken);

        return snapshot?.MealieConnection;
    }

    public async Task<IReadOnlyCollection<MealieConnectionEvent>> CreateAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MealieConnectionInitialized created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new Mealie connection stream must be MealieConnectionInitialized."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty Mealie connection event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);
        session.Store(new MealieConnectionIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MealieConnectionId id, IReadOnlyCollection<MealieConnectionEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty Mealie connection event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<MealieConnectionId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<MealieConnectionIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new MealieConnectionId(doc.Id);
    }
}
