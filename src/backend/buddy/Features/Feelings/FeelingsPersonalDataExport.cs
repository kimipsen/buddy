using buddy.Common.Erasure;

namespace buddy.Features.Feelings;

// The "feelings" section: each child's every check-in (oldest first), the prompt times, the display
// mode and the share links created for the journal, live or not. No share-link token hashes.
public sealed class FeelingsPersonalDataExporter(IFeelingsJournalEventStore journals, IFeelingsShareTokenEventStore shareTokens) : IPersonalDataExporter
{
    public Type Store => typeof(IFeelingsStore);

    public string Section => "feelings";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedFeelingsJournal> children = [];

        foreach (var childId in subject.Children)
        {
            var journal = FeelingCheckInsResponse.ForRange(
                await journals.FindSnapshotAsync(FeelingsJournalId.ForChild(childId), cancellationToken), childId, DateOnly.MinValue, DateOnly.MaxValue);
            var links = await shareTokens.ListForChildAsync(childId, cancellationToken);

            children.Add(new ExportedFeelingsJournal(
                childId.Value,
                journal.PromptTimes,
                journal.DisplayMode,
                journal.CheckIns,
                [.. links.OrderBy(l => l.CreatedAt).Select(l => new ExportedFeelingsShareLink(l.Id, l.CreatedAt, l.ExpiresAt, l.IsRevoked))]));
        }

        return children;
    }
}

public sealed record ExportedFeelingsJournal(
    Guid ChildId,
    IReadOnlyList<TimeOnly> PromptTimes,
    FeelingsDisplayMode DisplayMode,
    IReadOnlyList<FeelingCheckInResponse> CheckIns,
    IReadOnlyList<ExportedFeelingsShareLink> ShareLinks);

public sealed record ExportedFeelingsShareLink(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool IsRevoked);
