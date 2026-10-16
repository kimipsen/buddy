using buddy.Common.FeatureFlags;
using buddy.Features.Users;
using buddy.Features.VisitSummaries;

namespace buddy.Features.SleepDiaries;

// The sleep section of a doctor visit summary: the same entries and hygiene notes
// ListSleepDiaryEntries returns, for the summary's period. VisitSummaries has already authorized
// the reader and logs the read (docs/backend/analysis/doctor-visit-summary.md, Question 3).
public sealed class SleepDiaryVisitSummarySource(ISleepDiaryEventStore diaries) : IVisitSummarySectionSource
{
    public VisitSummarySection Section => VisitSummarySection.Sleep;

    public bool IsOffered(InstallationFeatures features) => features.SleepDiary;

    public async Task<VisitSummarySectionContent> ReadAsync(UserId childId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var diary = await diaries.FindSnapshotAsync(SleepDiaryId.ForChild(childId), cancellationToken);

        return SleepDiaryEntriesResponse.From(SleepDiaryRange.From(diary, from, to));
    }
}
