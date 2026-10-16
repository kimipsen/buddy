namespace buddy.Features.VisitSummaries;

// A part of the report a guardian can include. Each one is read by the feature that owns the data,
// through IVisitSummarySectionSource. Behaviour joins when the behaviour log ships (Question 7).
public enum VisitSummarySection
{
    Medicine,
    Sleep,
    Feelings
}
