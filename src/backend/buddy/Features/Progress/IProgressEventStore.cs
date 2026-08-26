namespace buddy.Features.Progress;

public interface IProgressEventStore
{
    Task<IReadOnlyCollection<ProgressEvent>> ReadAsync(ProgressId id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ProgressEvent>> CreateAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(ProgressId id, IReadOnlyCollection<ProgressEvent> events, CancellationToken cancellationToken);
}
