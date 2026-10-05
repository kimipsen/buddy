namespace buddy.Features.Babysitters;

public interface IBabysitterListEventStore
{
    Task<IReadOnlyCollection<BabysitterEvent>> ReadAsync(BabysitterListId id, CancellationToken cancellationToken);

    Task<BabysitterList?> FindSnapshotAsync(BabysitterListId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BabysitterEvent>> CreateAsync(BabysitterListId id, IReadOnlyCollection<BabysitterEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(BabysitterListId id, IReadOnlyCollection<BabysitterEvent> events, CancellationToken cancellationToken);
}
