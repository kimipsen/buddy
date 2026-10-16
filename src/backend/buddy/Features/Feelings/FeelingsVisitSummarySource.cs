using buddy.Common.FeatureFlags;
using buddy.Features.Users;
using buddy.Features.VisitSummaries;

namespace buddy.Features.Feelings;

// The feelings section of a doctor visit summary: the period's check-ins as ListFeelingCheckIns
// returns them, except that a note the child wrote themself is left out. The child wrote it for the
// family, not the doctor; zones, words and guardians' notes stay
// (docs/backend/analysis/doctor-visit-summary.md, Question 5).
public sealed class FeelingsVisitSummarySource(IFeelingsJournalEventStore journals) : IVisitSummarySectionSource
{
    public VisitSummarySection Section => VisitSummarySection.Feelings;

    public bool IsOffered(InstallationFeatures features) => features.Feelings;

    public async Task<VisitSummarySectionContent> ReadAsync(UserId childId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var journal = await journals.FindSnapshotAsync(FeelingsJournalId.ForChild(childId), cancellationToken);
        var range = FeelingCheckInsResponse.ForRange(journal, childId, from, to);

        return range with
        {
            CheckIns = [.. range.CheckIns.Select(c => c.IsSelfReport ? c with { Note = "" } : c)]
        };
    }
}
