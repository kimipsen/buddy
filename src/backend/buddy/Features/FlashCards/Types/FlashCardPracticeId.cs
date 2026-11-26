using buddy.Features.Users;

namespace buddy.Features.FlashCards;

// Deliberately equal to the child's own UserId -- a FlashCardPractice is a genuine 1:1 with its
// child, like ProgressId, so no index document is needed to find "this child's practice".
public sealed record FlashCardPracticeId(Guid Value)
{
    public static FlashCardPracticeId ForChild(UserId childId) => new(childId.Value);
}
