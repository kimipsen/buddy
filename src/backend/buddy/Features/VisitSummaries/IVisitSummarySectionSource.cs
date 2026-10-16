using buddy.Common.FeatureFlags;
using buddy.Features.Feelings;
using buddy.Features.Medicines;
using buddy.Features.SleepDiaries;
using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

// The one port between the report and the features it reads. Each source lives in the feature that
// owns the data (SleepDiaryVisitSummarySource, ...) and returns that feature's own response DTOs, so
// VisitSummaries never touches another feature's event store and the frontend reuses the types and
// components it already has (docs/backend/analysis/doctor-visit-summary.md, Question 3).
//
// A source does no authorization: the caller has already resolved the guardian or the share link.
public interface IVisitSummarySectionSource
{
    VisitSummarySection Section { get; }

    // A section whose feature the operator switched off is left out of every report.
    bool IsOffered(InstallationFeatures features);

    Task<VisitSummarySectionContent> ReadAsync(UserId childId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public union VisitSummarySectionContent(SleepDiaryEntriesResponse, VisitSummaryMedicineSection, FeelingCheckInsResponse);
