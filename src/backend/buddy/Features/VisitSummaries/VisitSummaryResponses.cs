using buddy.Features.Feelings;
using buddy.Features.Medicines;
using buddy.Features.SleepDiaries;

namespace buddy.Features.VisitSummaries;

// A summary as the guardian edits it, with its live links (never a token or hash).
public sealed record VisitSummaryResponse(
    Guid Id,
    Guid ChildId,
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<VisitSummarySection> Sections,
    string Note,
    Guid CreatedBy,
    DateTimeOffset LastModifiedAt,
    IReadOnlyList<VisitSummaryShareLinkSummary> ShareLinks)
{
    public static VisitSummaryResponse FromSummary(VisitSummary summary, DateTimeOffset now) => new(
        summary.Id.Value,
        summary.ChildId.Value,
        summary.Details.Title,
        summary.Details.VisitDate,
        summary.Details.From,
        summary.Details.To,
        summary.Details.Sections,
        summary.Details.Note,
        summary.CreatedBy.Value,
        summary.LastModifiedAt,
        [.. summary.LiveLinks(now).OrderByDescending(l => l.CreatedAt).Select(VisitSummaryShareLinkSummary.From)]);
}

public sealed record VisitSummaryShareLinkSummary(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt)
{
    public static VisitSummaryShareLinkSummary From(VisitSummaryShareLink link) => new(link.Id.Value, link.CreatedAt, link.ExpiresAt);
}

// The report itself: identical for the guardian and the doctor. A section is null when it wasn't
// chosen or its feature is switched off; the frontend computes every average and grouping from the
// sections' own DTOs (Question 3).
public sealed record VisitSummaryReport(
    string ChildGivenName,
    string ChildFamilyName,
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    string Note,
    string PreparedBy,
    SleepDiaryEntriesResponse? Sleep,
    VisitSummaryMedicineSection? Medicine,
    FeelingCheckInsResponse? Feelings);

public sealed record VisitSummaryWithReportResponse(VisitSummaryResponse Summary, VisitSummaryReport Report);

public sealed record SharedVisitSummaryResponse(VisitSummaryReport Report, DateTimeOffset? ExpiresAt);

// The plaintext token is returned exactly once, here.
public sealed record IssuedVisitSummaryShareLink(VisitSummaryShareLinkId Id, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);
