namespace buddy.Features.FirstThen;

public interface IFirstThenBoardEventStore
{
    // Empty for a child who has never had a board -- the stream is created lazily.
    Task<IReadOnlyCollection<FirstThenEvent>> ReadAsync(FirstThenBoardId id, CancellationToken cancellationToken);

    Task<FirstThenBoard?> FindSnapshotAsync(FirstThenBoardId id, CancellationToken cancellationToken);

    Task CreateAsync(FirstThenBoardId id, IReadOnlyCollection<FirstThenEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(FirstThenBoardId id, IReadOnlyCollection<FirstThenEvent> events, CancellationToken cancellationToken);
}
