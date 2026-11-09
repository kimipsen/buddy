using buddy.Common;
using buddy.Features.Babysitters;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.NightDuty;

// The editor's read: every fixed-schedule version, flagged past/current/upcoming against the
// child's today. Manage only -- the child reads resolved nights through ListNights, not the plan
// for weeks ahead (docs/backend/analysis/night-duty.md, Question 6).
public static class GetNightScheduleHandler
{
    public static async Task<Result<NightScheduleResponse>> Handle(
        GetNightSchedule query,
        INightScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        IBabysitterListEventStore babysitters,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var access = await NightDutyAuthorization.CheckManage(query.ChildId, query.UserId, guardians, cancellationToken);

        if (access != NightDutyAccess.Allowed)
        {
            return access.ToDeniedResult<NightScheduleResponse>();
        }

        if (await UserLocalDate.TodayAsync(query.ChildId, users, time, cancellationToken) is not { } today)
        {
            return new Result<NightScheduleResponse>.NotFound();
        }

        var schedule = await store.FindSnapshotAsync(NightScheduleId.ForChild(query.ChildId), cancellationToken)
            ?? NightSchedule.Empty(query.ChildId);
        var names = await BabysitterNames.LoadAsync(
            schedule.Patterns.SelectMany(p => p.Days.Select(d => d.Entry)).BabysitterOwners(), babysitters, cancellationToken);

        return new Result<NightScheduleResponse>.Success(NightScheduleResponse.From(schedule, today, names));
    }
}
