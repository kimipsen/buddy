using buddy.Features.Users;

namespace buddy.Features.Medicines;

public interface IMedicineSharingEventStore
{
    Task<IReadOnlyCollection<MedicineSharingEvent>> ReadAsync(MedicineSharingId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<MedicineSharingEvent>> CreateAsync(MedicineSharingId id, IReadOnlyCollection<MedicineSharingEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(MedicineSharingId id, IReadOnlyCollection<MedicineSharingEvent> events, CancellationToken cancellationToken);

    // A MedicineSharing is a 1:1 singleton per child, provisioned lazily -- null means the child
    // has never shared their medicine schedules with any group.
    Task<MedicineSharingId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}
