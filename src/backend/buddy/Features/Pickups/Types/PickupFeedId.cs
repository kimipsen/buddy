using buddy.Features.Users;

namespace buddy.Features.Pickups;

// Deliberately equal to the guardian's own UserId -- a guardian has exactly one pickup feed (same
// reasoning as WorkLocationScheduleId.ForGuardian), so no index document is needed. The id is in
// the feed URL; the token, not the id, is the credential. See
// docs/backend/analysis/pickup-ical-feed.md, Question 1.
public sealed record PickupFeedId(Guid Value)
{
    public static PickupFeedId ForGuardian(UserId guardianId) => new(guardianId.Value);
}
