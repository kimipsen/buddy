using buddy.Common.Concurrency;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Medicines;

public sealed class MartenMedicineEventStore(IMedicinesStore store) : IMedicineEventStore
{
    public async Task<IReadOnlyCollection<MedicineEvent>> ReadAsync(MedicineId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => MedicineEvent.FromPayload(e.Data))];
    }

    public async Task<MedicineSchedule?> FindSnapshotAsync(MedicineId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<MedicineScheduleSnapshot>(id.Value, cancellationToken);

        return snapshot?.MedicineSchedule;
    }

    public async Task<IReadOnlyCollection<MedicineEvent>> CreateAsync(MedicineId id, IReadOnlyCollection<MedicineEvent> events, CancellationToken cancellationToken)
    {
        var childId = events.FirstOrDefault() switch
        {
            MedicineScheduleCreated created => created.ChildId,
            _ => throw new InvalidOperationException("The first event of a new medicine schedule stream must be MedicineScheduleCreated."),
        };

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty medicine event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, payloads);
        session.Store(new MedicineIndexDocument(id.Value, childId.Value));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(MedicineId id, IReadOnlyCollection<MedicineEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty medicine event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, payloads);

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<MedicineId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        var docs = await session.Query<MedicineIndexDocument>()
            .Where(d => d.ChildId == childId.Value)
            .ToListAsync(cancellationToken);

        return [.. docs.Select(d => new MedicineId(d.Id))];
    }
}
