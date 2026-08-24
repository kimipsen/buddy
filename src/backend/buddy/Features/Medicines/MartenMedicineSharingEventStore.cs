using buddy.Features.Users;

using Marten;

namespace buddy.Features.Medicines;

public sealed class MartenMedicineSharingEventStore(IMedicinesStore store) : IMedicineSharingEventStore
{
    public async Task<IReadOnlyCollection<MedicineSharingEvent>> ReadAsync(MedicineSharingId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);

        return [.. events.Select(e => MedicineSharingEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<MedicineSharingEvent>> CreateAsync(MedicineSharingId id, IReadOnlyCollection<MedicineSharingEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MedicineSharedWithGroup shared => shared.ChildId,
            _ => throw new InvalidOperationException("The first event of a new medicine sharing stream must be MedicineSharedWithGroup."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty medicine sharing event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(id.Value, payloads);
        session.Store(new MedicineSharingIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MedicineSharingId id, IReadOnlyCollection<MedicineSharingEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty medicine sharing event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<MedicineSharingId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var doc = await session.Query<MedicineSharingIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : new MedicineSharingId(doc.Id);
    }
}
