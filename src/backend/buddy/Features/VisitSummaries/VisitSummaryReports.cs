using System.Diagnostics;

using buddy.Common.FeatureFlags;
using buddy.Features.Feelings;
using buddy.Features.Medicines;
using buddy.Features.SleepDiaries;
using buddy.Features.Users;

using Microsoft.Extensions.Options;

namespace buddy.Features.VisitSummaries;

// Builds the report for a summary from current data, every time: nothing of it is stored
// (Question 4). Shared by the guardian's GetVisitSummary and the anonymous GetSharedVisitSummary.
public sealed class VisitSummaryReports(
    IEnumerable<IVisitSummarySectionSource> sources,
    IUserEventStore users,
    IOptions<FeatureOptions> features)
{
    private readonly IReadOnlyList<IVisitSummarySectionSource> _sources = [.. sources];

    // Null when the child no longer exists.
    public async Task<VisitSummaryReport?> BuildAsync(VisitSummary summary, CancellationToken cancellationToken)
    {
        var child = await users.FindSnapshotAsync(summary.ChildId, cancellationToken);

        if (child is null || child.IsDeleted)
        {
            return null;
        }

        var preparedBy = await users.FindSnapshotAsync(summary.CreatedBy, cancellationToken) is { IsDeleted: false } guardian
            ? $"{guardian.Name.GivenName} {guardian.Name.FamilyName}".Trim()
            : "";

        var offered = features.Value.Effective();
        var details = summary.Details;
        SleepDiaryEntriesResponse? sleep = null;
        VisitSummaryMedicineSection? medicine = null;
        FeelingCheckInsResponse? feelings = null;

        foreach (var source in _sources.Where(s => details.Sections.Contains(s.Section) && s.IsOffered(offered)))
        {
            // An expression, so a new section's union case can't be forgotten here (CS8509).
            (sleep, medicine, feelings) = await source.ReadAsync(summary.ChildId, details.From, details.To, cancellationToken) switch
            {
                SleepDiaryEntriesResponse content => (content, medicine, feelings),
                VisitSummaryMedicineSection content => (sleep, content, feelings),
                FeelingCheckInsResponse content => (sleep, medicine, content),
                null => throw new UnreachableException($"Section source {source.Section} returned no content."),
            };
        }

        return new VisitSummaryReport(
            child.Name.GivenName,
            child.Name.FamilyName,
            details.Title,
            details.VisitDate,
            details.From,
            details.To,
            details.Note,
            preparedBy,
            sleep,
            medicine,
            feelings);
    }
}
