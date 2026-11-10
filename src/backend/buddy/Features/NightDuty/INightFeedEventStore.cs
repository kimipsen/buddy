namespace buddy.Features.NightDuty;

public interface INightFeedEventStore
{
    Task<IReadOnlyCollection<NightFeedEvent>> ReadAsync(NightFeedId id, CancellationToken cancellationToken);

    Task<NightFeed?> FindSnapshotAsync(NightFeedId id, CancellationToken cancellationToken);

    Task CreateAsync(NightFeedId id, IReadOnlyCollection<NightFeedEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(NightFeedId id, IReadOnlyCollection<NightFeedEvent> events, CancellationToken cancellationToken);
}
