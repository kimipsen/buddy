using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class StopMedicineScheduleHandler
{
    public static async Task<Result<Unit>> Handle(
        StopMedicineSchedule command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var events = await medicines.ReadAsync(command.MedicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != command.ChildId)
        {
            return new Result<Unit>.NotFound();
        }

        await medicines.AppendAsync(command.MedicineId, [new MedicineScheduleStopped(command.MedicineId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
