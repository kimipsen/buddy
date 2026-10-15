using buddy.Common.Concurrency;

namespace buddy.Features.Medicines;

public sealed class MartenMedicineEffectLogEventStore(IMedicinesStore store) : IMedicineEffectLogEventStore
{
    public async Task<IReadOnlyCollection<MedicineEffectLogEvent>> ReadAsync(MedicineEffectLogId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(id.Value, token: cancellationToken);
        session.ObserveStream(id.Value, events);

        return [.. events.Select(e => MedicineEffectLogEvent.FromPayload(e.Data))];
    }

    public async Task<MedicineEffectLog?> FindSnapshotAsync(MedicineEffectLogId id, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var snapshot = await session.LoadAsync<MedicineEffectLogSnapshot>(id.Value, cancellationToken);

        return snapshot?.MedicineEffectLog;
    }

    public async Task CreateAsync(MedicineEffectLogId id, IReadOnlyCollection<MedicineEffectLogEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not MedicineEffectLogStarted)
        {
            throw new InvalidOperationException("The first event of a new medicine effect log stream must be MedicineEffectLogStarted.");
        }

        await using var session = store.LightweightSession();
        session.StartTrackedStream(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AppendAsync(MedicineEffectLogId id, IReadOnlyCollection<MedicineEffectLogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        await using var session = store.LightweightSession();
        session.AppendTracked(id.Value, ToPayloads(events));

        await session.SaveChangesAsync(cancellationToken);
    }

    private static object[] ToPayloads(IReadOnlyCollection<MedicineEffectLogEvent> events) =>
        [.. events.Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty medicine effect log event."))];
}
