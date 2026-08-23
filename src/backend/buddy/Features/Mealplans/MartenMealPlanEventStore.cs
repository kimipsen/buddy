using buddy.Features.Users;

using Marten;

namespace buddy.Features.Mealplans;

public sealed class MartenMealPlanEventStore(IMealplansStore store) : IMealPlanEventStore
{
    public async Task<IReadOnlyCollection<MealPlanEvent>> ReadAsync(MealPlanId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => MealPlanEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<MealPlanEvent>> CreateAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MealPlanCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new meal plan stream must be MealPlanCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new MealPlanIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MealPlanId id, IReadOnlyCollection<MealPlanEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty meal plan event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<MealPlanId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<MealPlanIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new MealPlanId(doc.Id);
    }
}
