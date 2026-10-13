namespace buddy.Features.Feelings;

// The one write shape every journal command shares: start the stream lazily with
// FeelingsJournalStarted in the same CreateAsync as the first change, append otherwise. The caller
// has already read the stream (ReadAsync), so an append is an expected-version append.
internal static class FeelingsJournalWrites
{
    public static async Task WriteAsync(
        IFeelingsJournalEventStore journals,
        FeelingsJournalId id,
        FeelingsJournal? journal,
        FeelingsJournalStarted started,
        FeelingsJournalEvent change,
        CancellationToken cancellationToken)
    {
        if (journal is null)
        {
            await journals.CreateAsync(id, [started, change], cancellationToken);
        }
        else
        {
            await journals.AppendAsync(id, [change], cancellationToken);
        }
    }
}
