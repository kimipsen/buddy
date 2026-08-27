using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class RescheduleMedicineHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        RescheduleMedicine command,
        IValidator<RescheduleMedicine> validator,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new Result<MedicineSchedule>.Validation(problem);
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
