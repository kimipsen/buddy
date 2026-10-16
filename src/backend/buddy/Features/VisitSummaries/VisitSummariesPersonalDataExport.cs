using buddy.Common.Erasure;

namespace buddy.Features.VisitSummaries;

// The "visitSummaries" section: each child's summaries (deleted ones too, marked) and every link
// created for them, live or not. Never a token hash, and no report: the report is the sleep diary,
// medicine and feelings data, which their own sections already export.
public sealed class VisitSummariesPersonalDataExporter(IVisitSummaryEventStore summaries) : IPersonalDataExporter
{
    public Type Store => typeof(IVisitSummariesStore);

    public string Section => "visitSummaries";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedVisitSummary> exported = [];

        foreach (var childId in subject.Children)
        {
            foreach (var document in await summaries.ListForChildAsync(childId, cancellationToken))
            {
                if (await summaries.FindSnapshotAsync(new VisitSummaryId(document.Id), cancellationToken) is not { } summary)
                {
                    continue;
                }

                var details = summary.Details;
                exported.Add(new ExportedVisitSummary(
                    summary.Id.Value,
                    childId.Value,
                    details.Title,
                    details.VisitDate,
                    details.From,
                    details.To,
                    details.Sections,
                    details.Note,
                    summary.IsDeleted,
                    [.. summary.ShareLinks.Select(l => new ExportedVisitSummaryShareLink(l.Id.Value, l.CreatedAt, l.ExpiresAt, l.IsRevoked))]));
            }
        }

        return exported;
    }
}

public sealed record ExportedVisitSummary(
    Guid Id,
    Guid ChildId,
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<VisitSummarySection> Sections,
    string Note,
    bool IsDeleted,
    IReadOnlyList<ExportedVisitSummaryShareLink> ShareLinks);

public sealed record ExportedVisitSummaryShareLink(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool IsRevoked);
