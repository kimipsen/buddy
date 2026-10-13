using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// Deliberately equal to the child's own UserId, like ProgressId: a child has exactly one set of
// warning settings, so "this child's settings" is a direct stream load with no index document
// (docs/backend/analysis/transition-warnings.md, Question 2).
public sealed record TransitionWarningSettingsId(Guid Value)
{
    public static TransitionWarningSettingsId ForChild(UserId childId) => new(childId.Value);
}
