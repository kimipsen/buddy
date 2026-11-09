using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// Deliberately equal to the child's own UserId -- a NightSchedule is a genuine 1:1 relationship
// with its child (same reasoning as ProgressId.ForChild and WorkLocationScheduleId.ForGuardian), so
// no index document is needed to find "this child's schedule". See
// docs/backend/analysis/night-duty.md, Question 2.
public sealed record NightScheduleId(Guid Value)
{
    public static NightScheduleId ForChild(UserId childId) => new(childId.Value);
}
