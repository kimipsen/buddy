using buddy.Common.Erasure;

namespace buddy.Features.SleepDiaries;

// The "sleepDiary" section: each child's whole diary (every logged night and the hygiene notes)
// and the share links created for it, live or not. No share-link token hashes.
public sealed class SleepDiariesPersonalDataExporter(ISleepDiaryEventStore diaries, ISleepDiaryShareTokenEventStore shareTokens) : IPersonalDataExporter
{
    public Type Store => typeof(ISleepDiariesStore);

    public string Section => "sleepDiary";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedSleepDiary> children = [];

        foreach (var childId in subject.Children)
        {
            var diary = SleepDiaryRange.From(
                await diaries.FindSnapshotAsync(SleepDiaryId.ForChild(childId), cancellationToken), DateOnly.MinValue, DateOnly.MaxValue);
            var links = await shareTokens.ListForChildAsync(childId, cancellationToken);

            children.Add(new ExportedSleepDiary(
                childId.Value,
                diary.SleepHygieneNotes,
                [.. diary.Entries.Select(SleepEntryResponse.From)],
                [.. links.OrderBy(l => l.CreatedAt).Select(l => new ExportedShareLink(l.Id, l.CreatedAt, l.ExpiresAt, l.IsRevoked))]));
        }

        return children;
    }
}

public sealed record ExportedSleepDiary(
    Guid ChildId,
    string SleepHygieneNotes,
    IReadOnlyList<SleepEntryResponse> Entries,
    IReadOnlyList<ExportedShareLink> ShareLinks);

public sealed record ExportedShareLink(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool IsRevoked);
