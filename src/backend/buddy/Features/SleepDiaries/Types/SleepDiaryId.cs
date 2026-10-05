using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

// Deliberately equal to the child's own UserId -- a child has exactly one sleep diary, a genuine
// 1:1 relationship (same as ProgressId), so finding "this child's diary" needs no index document.
// See docs/backend/analysis/sleep-diary.md, Question 2.
public sealed record SleepDiaryId(Guid Value)
{
    public static SleepDiaryId ForChild(UserId childId) => new(childId.Value);
}
