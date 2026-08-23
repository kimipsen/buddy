using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenMealEventStore(IMealplansStore store) : IMealEventStore
{
    public async Task<IReadOnlyCollection<MealEvent>> ReadAsync(MealId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => MealEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<MealEvent>> CreateAsync(MealId id, IReadOnlyCollection<MealEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MealCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new meal stream must be MealCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new MealIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MealId id, IReadOnlyCollection<MealEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MealId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var docs = await session.Query<MealIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .ToListAsync(cancellationToken);

        return [.. docs.Select(d => new MealId(d.Id))];
    }
}
