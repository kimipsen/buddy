using buddy.Common;
using buddy.Common.Security;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

public static class GetNightIcalFeedHandler
{
    // The pickup and meal-plan feeds' window: nights are planned a week or two out, and a short look
    // back keeps last week's nights visible (docs/backend/analysis/night-duty-ical-feed.md, Question 2).
    private const int LookBehindDays = 14;
    private const int LookAheadDays = 60;

    public static async Task<Result<string>> Handle(
        GetNightIcalFeed query,
        INightFeedEventStore feeds,
        INightScheduleEventStore schedules,
        IBabysitterListEventStore babysitters,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        // Same outcome (no feed) whether the feed doesn't exist, the token is wrong or revoked, the
        // guardian's account is gone, or they are no longer a guardian of the token's child -- an
        // anonymous request can't tell which.
        if (await feeds.FindSnapshotAsync(query.FeedId, cancellationToken) is not { } feed
            || feed.FindMatchingToken(ShareSecret.Hash(query.Token)) is not { } token)
        {
            return new Result<string>.NotFound();
        }

        if (await users.FindSnapshotAsync(feed.GuardianId, cancellationToken) is not { IsDeleted: false } guardian
            || await NightDutyAuthorization.CheckManage(token.ChildId, feed.GuardianId, guardians, cancellationToken) != NightDutyAccess.Allowed
            || await users.FindSnapshotAsync(token.ChildId, cancellationToken) is not { IsDeleted: false } child)
        {
            return new Result<string>.NotFound();
        }

        // Dates are the child's wall-clock dates, so the window is centred on the child's today.
        var today = UserLocalDate.Today(child, time);
        var nights = await NightExpansion.ExpandAsync(
            token.ChildId, today.AddDays(-LookBehindDays), today.AddDays(LookAheadDays), schedules, babysitters, cancellationToken);

        var guardianNames = await GuardianNamesAsync(nights, users, cancellationToken);

        return new Result<string>.Success(NightIcalFeedWriter.Write(
            nights,
            new NightFeedContext(token.ChildId, child.Name.GivenName, guardianNames, guardian.Language),
            time));
    }

    // What the child calls each guardian the planned nights name (CallingName, else the given name),
    // one read per distinct guardian. A deleted guardian is left out, and the writer says "a former
    // guardian".
    private static async Task<IReadOnlyDictionary<Guid, string>> GuardianNamesAsync(
        IEnumerable<Night> nights, IUserEventStore users, CancellationToken cancellationToken)
    {
        var ids = nights
            .Select(night => night.Status)
            .OfType<NightStatus.NightPlanned>()
            .Select(planned => planned.Entry.Responsible)
            .OfType<GuardianNightResponsibleDto>()
            .Select(guardian => guardian.GuardianId)
            .Distinct();

        var names = new Dictionary<Guid, string>();

        foreach (var id in ids)
        {
            if (await users.FindSnapshotAsync(new UserId(id), cancellationToken) is { IsDeleted: false } user)
            {
                names[id] = user.CallingName.Length > 0 ? user.CallingName : user.Name.GivenName;
            }
        }

        return names;
    }
}
