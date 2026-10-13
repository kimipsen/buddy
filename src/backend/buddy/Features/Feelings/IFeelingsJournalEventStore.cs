namespace buddy.Features.Feelings;

public interface IFeelingsJournalEventStore
{
    // Empty for a child whose journal has never been written to -- the stream is created lazily.
    Task<IReadOnlyCollection<FeelingsJournalEvent>> ReadAsync(FeelingsJournalId id, CancellationToken cancellationToken);

    Task<FeelingsJournal?> FindSnapshotAsync(FeelingsJournalId id, CancellationToken cancellationToken);

    Task CreateAsync(FeelingsJournalId id, IReadOnlyCollection<FeelingsJournalEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(FeelingsJournalId id, IReadOnlyCollection<FeelingsJournalEvent> events, CancellationToken cancellationToken);
}
