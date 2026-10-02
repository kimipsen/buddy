using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.WorkLocations;

public static class GetWorkLocationScheduleHandler
{
    public static async Task<Result<WorkLocationScheduleResponse>> Handle(
        GetWorkLocationSchedule query,
        IWorkLocationScheduleEventStore store,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var access = await WorkLocationAuthorization.CheckView(query.GuardianId, userId, guardians, cancellationToken);

        if (access != WorkLocationAccess.Allowed)
        {
            return access.ToDeniedResult<WorkLocationScheduleResponse>();
        }

        // No stream yet reads as the same empty schedule WorkLocationScheduleStarted would fold to.
        var schedule = await store.FindSnapshotAsync(WorkLocationScheduleId.ForGuardian(query.GuardianId), cancellationToken)
            ?? WorkLocationSchedule.Empty(query.GuardianId, DateTimeOffset.UtcNow);

        return new Result<WorkLocationScheduleResponse>.Success(WorkLocationScheduleResponse.From(schedule));
    }
}
