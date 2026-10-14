namespace buddy.Features.Medicines;

public interface IMedicineEffectLogEventStore
{
    // Empty for a child with no effect notes yet -- the stream is created lazily.
    Task<IReadOnlyCollection<MedicineEffectLogEvent>> ReadAsync(MedicineEffectLogId id, CancellationToken cancellationToken);

    Task<MedicineEffectLog?> FindSnapshotAsync(MedicineEffectLogId id, CancellationToken cancellationToken);

    Task CreateAsync(MedicineEffectLogId id, IReadOnlyCollection<MedicineEffectLogEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MedicineEffectLogId id, IReadOnlyCollection<MedicineEffectLogEvent> events, CancellationToken cancellationToken);
}
