namespace buddy.Features.Progress;

public interface IProgressEventStore
{
    Task<IReadOnlyCollection<ProgressEvent>> ReadAsync(ProgressId id, CancellationToken cancellationToken);

    Task<ChildProgress?> FindSnapshotAsync(ProgressId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProgressEvent>> CreateAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken);

    // The award a finished game wrote to this child's progress stream, if any. A typed event query
    // scoped to the child's stream (never a stream read, never another child's events); an untracked
    // read, so it cannot move the expected version of the append that follows.
    Task<FlashCardStarsAwarded?> FindFlashCardAwardAsync(ProgressId id, Guid gameId, CancellationToken cancellationToken);

    Task AppendAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken);
}
