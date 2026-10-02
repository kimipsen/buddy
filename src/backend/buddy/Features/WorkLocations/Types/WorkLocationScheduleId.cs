using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

// Deliberately equal to the guardian's own UserId -- a WorkLocationSchedule is a genuine 1:1
// relationship with its guardian (same reasoning as ProgressId.ForChild), so no index document is
// needed to find "this guardian's schedule". See docs/backend/analysis/work-locations.md, Question 2.
public sealed record WorkLocationScheduleId(Guid Value)
{
    public static WorkLocationScheduleId ForGuardian(UserId guardianId) => new(guardianId.Value);
}
