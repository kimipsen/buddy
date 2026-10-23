namespace buddy.Features.Pickups;

public interface IPickupFeedEventStore
{
    Task<IReadOnlyCollection<PickupFeedEvent>> ReadAsync(PickupFeedId id, CancellationToken cancellationToken);

    Task<PickupFeed?> FindSnapshotAsync(PickupFeedId id, CancellationToken cancellationToken);

    Task CreateAsync(PickupFeedId id, IReadOnlyCollection<PickupFeedEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(PickupFeedId id, IReadOnlyCollection<PickupFeedEvent> events, CancellationToken cancellationToken);
}
