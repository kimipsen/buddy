using buddy.Common;
using buddy.Common.Observability;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

public static class GetMyFeelingsTodayHandler
{
    public static async Task<Result<MyFeelingsTodayResponse>> Handle(
        GetMyFeelingsToday query,
        IFeelingsJournalEventStore journals,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        ILogger<GetMyFeelingsToday> logger,
        CancellationToken cancellationToken)
    {
        var access = await FeelingsAuthorization.CheckSelf(query.UserId, guardians, cancellationToken);

        if (access != FeelingsAccess.Allowed)
        {
            return access.ToDeniedResult<MyFeelingsTodayResponse>();
        }

        if (await ChildLocalClock.NowAsync(query.UserId, users, time, cancellationToken) is not { } now)
        {
            return new Result<MyFeelingsTodayResponse>.NotFound();
        }

        var journal = await journals.FindSnapshotAsync(FeelingsJournalId.ForChild(query.UserId), cancellationToken);

        logger.CheckInsRead(query.UserId.Value, now.Date, now.Date, query.UserId.Value, HealthDataAccessPath.Self);

        // Today's own self-reports only: no history, and never a guardian's observation about the
        // child (Question 5, settled by the user 2026-10-11 and 2026-10-13).
        return new Result<MyFeelingsTodayResponse>.Success(journal is null
            ? new MyFeelingsTodayResponse(now.Date, [], FeelingsDisplayMode.FacesAndWords, [])
            : new MyFeelingsTodayResponse(
                now.Date,
                journal.PromptTimes,
                journal.DisplayMode,
                [.. journal.Between(now.Date, now.Date)
                    .Where(c => c.IsSelfReportOf(query.UserId))
                    .Select(c => FeelingCheckInResponse.From(c, query.UserId))]));
    }
}
