using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// Deliberately equal to the guardian's own UserId -- a guardian has exactly one night feed (holding
// a token per subscribed child), the PickupFeedId.ForGuardian shape, so no index document is
// needed. The id is in the feed URL; the token, not the id, is the credential. See
// docs/backend/analysis/night-duty-ical-feed.md, Question 1.
public sealed record NightFeedId(Guid Value)
{
    public static NightFeedId ForGuardian(UserId guardianId) => new(guardianId.Value);
}
