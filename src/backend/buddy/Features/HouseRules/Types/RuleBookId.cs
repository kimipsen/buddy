using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.HouseRules;

// Deliberately equal to the scope's own id -- a child or a group has exactly one rule book, so
// "find this child's/group's rules" is a direct load with no index document (same 1:1 trick as
// SleepDiaryId and ProgressId). See docs/backend/analysis/house-rules.md, Question 2.
public sealed record RuleBookId(Guid Value)
{
    public static RuleBookId ForChild(UserId childId) => new(childId.Value);

    public static RuleBookId ForGroup(GroupId groupId) => new(groupId.Value);
}
