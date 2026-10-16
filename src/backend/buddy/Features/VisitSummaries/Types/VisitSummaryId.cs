namespace buddy.Features.VisitSummaries;

// Many summaries per child (one per prepared visit), so a UUIDv7 with an index document rather than
// the SleepDiaryId.ForChild trick (docs/backend/analysis/doctor-visit-summary.md, Question 2).
public sealed record VisitSummaryId(Guid Value)
{
    public static VisitSummaryId New() => new(Guid.CreateVersion7());
}

public sealed record VisitSummaryShareLinkId(Guid Value)
{
    public static VisitSummaryShareLinkId New() => new(Guid.CreateVersion7());
}
