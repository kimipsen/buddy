using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// Deliberately equal to the child's own UserId -- a child has at most one current board, so the
// stream holds every board the child has had and "this child's board" needs no index document
// (same as SleepDiaryId). See docs/backend/analysis/first-then-board.md, Question 2.
public sealed record FirstThenBoardId(Guid Value)
{
    public static FirstThenBoardId ForChild(UserId childId) => new(childId.Value);
}
