using buddy.Features.Users;

namespace buddy.Features.Progress;

// Deliberately equal to the child's own UserId -- a ChildProgress is a genuine 1:1 relationship
// with its child, unlike e.g. MedicineId -> ChildId, so no separate index document is needed to
// find "this child's progress."
public sealed record ProgressId(Guid Value)
{
    public static ProgressId ForChild(UserId childId) => new(childId.Value);
}
