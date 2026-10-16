namespace buddy.Features.VisitSummaries;

// What a guardian edits on a summary, as one value: created whole, overwritten whole by
// UpdateVisitSummary (Before/After in the event, as UpdateMedicineSchedule). Sections are kept
// distinct and in enum order, so two details with the same choices compare equal.
public sealed record VisitSummaryDetails(
    string Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<VisitSummarySection> Sections,
    string Note)
{
    public static IReadOnlyList<VisitSummarySection> Normalize(IEnumerable<VisitSummarySection> sections) =>
        [.. sections.Distinct().Order()];

    public bool HasSameContentAs(VisitSummaryDetails other) =>
        Title == other.Title
        && VisitDate == other.VisitDate
        && From == other.From
        && To == other.To
        && Sections.SequenceEqual(other.Sections)
        && Note == other.Note;
}
