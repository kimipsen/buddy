using buddy.Common;
using buddy.Common.Security;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Pickups;

public static class GetPickupIcalFeedHandler
{
    // The meal-plan feed's window: pickups are planned a week or two out, not a year out (see
    // docs/backend/analysis/pickup-ical-feed.md, Question 2).
    private const int LookBehindDays = 14;
    private const int LookAheadDays = 60;

    public static async Task<Result<string>> Handle(
        GetPickupIcalFeed query,
        IPickupFeedEventStore feeds,
        IPickupScheduleEventStore schedules,
        IBabysitterListEventStore babysitters,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // Same outcome (no feed) whether the feed doesn't exist, the token is wrong or revoked, or
        // the guardian's account is gone -- an anonymous request can't tell which.
        if (await feeds.FindSnapshotAsync(query.FeedId, cancellationToken) is not { } feed
            || feed.FindMatchingToken(ShareSecret.Hash(query.Token)) is null)
        {
            return new Result<string>.NotFound();
        }

        if (await users.FindSnapshotAsync(feed.GuardianId, cancellationToken) is not { IsDeleted: false } guardian)
        {
            return new Result<string>.NotFound();
        }

        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var from = today.AddDays(-LookBehindDays);
        var to = today.AddDays(LookAheadDays);

        var entries = new List<PickupFeedEntry>();

        // Active links only, resolved per request: a child the guardian was unlinked from drops out
        // of the feed on the next poll.
        foreach (var link in await guardians.ListForGuardianAsync(feed.GuardianId, cancellationToken))
        {
            var childId = new UserId(link.ChildId);

            if (await users.FindSnapshotAsync(childId, cancellationToken) is not { IsDeleted: false } child)
            {
                continue;
            }

            var occurrences = await PickupScheduleExpansion.ExpandAsync(childId, from, to, schedules, babysitters, cancellationToken);

            // Only the slots this guardian does themself -- not a co-guardian's, a sibling's, a
            // babysitter's (even one from this guardian's own list), a playdate's or self-escort.
            entries.AddRange(occurrences
                .Where(occurrence => occurrence.Assignee is GuardianAssigneeDto assignee && assignee.GuardianId == feed.GuardianId.Value)
                .Select(occurrence => new PickupFeedEntry(childId, child.Name.GivenName, child.TimeZoneId, occurrence)));
        }

        return new Result<string>.Success(PickupIcalFeedWriter.Write(entries, guardian.Language, time));
    }
}
