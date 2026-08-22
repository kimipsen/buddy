using buddy.Features.Users;

namespace buddy.Features.Medicines;

public interface IMedicineEventStore
{
    Task<IReadOnlyCollection<MedicineEvent>> ReadAsync(MedicineId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MedicineEvent>> CreateAsync(MedicineId id, IReadOnlyCollection<MedicineEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MedicineId id, IReadOnlyCollection<MedicineEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MedicineId>> ListIdsForChildAsync(UserId childId, CancellationToken cancellationToken);
}
