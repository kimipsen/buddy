using buddy.Common;
using buddy.Features.Guardians;

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

        var events = await medicines.ReadAsync(command.MedicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != command.ChildId)
        {
            return new Result<MedicineSchedule>.NotFound();
        }

        var before = new MedicineWindow(schedule.Times, schedule.StartDate, schedule.EndDate);
        var after = new MedicineWindow(command.Times, command.StartDate, command.EndDate);

        if (before == after)
        {
            return new Result<MedicineSchedule>.Success(schedule);
        }

        await medicines.AppendAsync(command.MedicineId, [new MedicineScheduleRescheduled(command.MedicineId, before, after, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule with { Times = command.Times, StartDate = command.StartDate, EndDate = command.EndDate, LastModifiedBy = userId });
    }
}
