using buddy.Features.Users;

namespace buddy.Features.Feelings;

// Deliberately equal to the child's own UserId -- a child has exactly one feelings journal, a 1:1
// relationship like SleepDiaryId and ProgressId, so "this child's journal" needs no index document
// (docs/backend/analysis/feelings-check-in.md, Question 3).
public sealed record FeelingsJournalId(Guid Value)
{
    public static FeelingsJournalId ForChild(UserId childId) => new(childId.Value);
}

public sealed record FeelingCheckInId(Guid Value)
{
    public static FeelingCheckInId New() => new(Guid.CreateVersion7());
}
