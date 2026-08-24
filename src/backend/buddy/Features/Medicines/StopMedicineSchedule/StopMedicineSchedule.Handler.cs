using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        var stopped = await StopForChildAsync(command.ChildId, command.MedicineId, userId, medicines, cancellationToken);

        return stopped ? new Result<Unit>.Success(Unit.Value) : new Result<Unit>.NotFound();
    }

    // Shared with StopMedicineScheduleForGroupHandler -- everything past authorization is
    // identical. False means no matching, non-stopped schedule for this child.
    internal static async Task<bool> StopForChildAsync(UserId childId, MedicineId medicineId, UserId modifiedBy, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var events = await medicines.ReadAsync(medicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != childId)
        {
            return false;
        }

        await medicines.AppendAsync(medicineId, [new MedicineScheduleStopped(medicineId, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return true;
    }
}
