using buddy.Features.Users;

namespace buddy.Features.Pickups;

public interface IPickupScheduleEventStore
{
    Task<IReadOnlyCollection<PickupEvent>> ReadAsync(PickupScheduleId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PickupEvent>> CreateAsync(PickupScheduleId id, IReadOnlyCollection<PickupEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(PickupScheduleId id, IReadOnlyCollection<PickupEvent> events, CancellationToken cancellationToken);

    // A PickupSchedule is a 1:1 singleton per child, provisioned lazily -- null means the child has
    // no schedule stream yet (nothing has ever been assigned).
    Task<PickupScheduleId?> FindIdForChildAsync(UserId childId, CancellationToken cancellationToken);
}
