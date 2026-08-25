using buddy.Features.Users;

using Marten;

namespace buddy.Features.Pickups;

public sealed class MartenPickupScheduleEventStore(IPickupsStore store) : IPickupScheduleEventStore
{
    public async Task<IReadOnlyCollection<PickupEvent>> ReadAsync(PickupScheduleId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => PickupEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<PickupEvent>> CreateAsync(PickupScheduleId id, IReadOnlyCollection<PickupEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            PickupScheduleCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new pickup schedule stream must be PickupScheduleCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty pickup event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new PickupScheduleIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(PickupScheduleId id, IReadOnlyCollection<PickupEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty pickup event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<PickupScheduleId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<PickupScheduleIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new PickupScheduleId(doc.Id);
    }
}
