using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

public static class RescheduleMedicineHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        RescheduleMedicine command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.Times.Count == 0)
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires at least one dose time.");
        }

        if (command.EndDate is { } end && end < command.StartDate)
        {
            return new Result<MedicineSchedule>.Validation("The end date cannot be before the start date.");
        }

        if (command.UserId is not { } userId)
        {
            return new Result<MedicineSchedule>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineSchedule>();
        }

        var result = await RescheduleForChildAsync(command.ChildId, command.MedicineId, userId, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);

        return result is null ? new Result<MedicineSchedule>.NotFound() : new Result<MedicineSchedule>.Success(result);
    }

    // Shared with RescheduleMedicineForGroupHandler -- everything past authorization is
    // identical. Null means no matching, non-stopped schedule for this child.
    internal static async Task<MedicineSchedule?> RescheduleForChildAsync(
        UserId childId, MedicineId medicineId, UserId modifiedBy, IReadOnlyList<TimeOnly> times, DateOnly startDate, DateOnly? endDate, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var events = await medicines.ReadAsync(medicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != childId)
        {
            return null;
        }

        var before = new MedicineWindow(schedule.Times, schedule.StartDate, schedule.EndDate);
        var after = new MedicineWindow(times, startDate, endDate);

        if (before == after)
        {
            return schedule;
        }

        await medicines.AppendAsync(medicineId, [new MedicineScheduleRescheduled(medicineId, before, after, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return schedule with { Times = times, StartDate = startDate, EndDate = endDate, LastModifiedBy = modifiedBy };
    }
}
