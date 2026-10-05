using buddy.Features.Users;

namespace buddy.Features.Babysitters;

// Deliberately equal to the guardian's own UserId -- one list per guardian, the same 1:1 trick as
// WorkLocationScheduleId.ForGuardian, so no index document is needed. See
// docs/backend/analysis/babysitters.md, Question 2.
public sealed record BabysitterListId(Guid Value)
{
    public static BabysitterListId ForGuardian(UserId guardianId) => new(guardianId.Value);
}
