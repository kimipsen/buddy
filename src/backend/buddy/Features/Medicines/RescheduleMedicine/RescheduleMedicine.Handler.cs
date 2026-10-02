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

        var userId = command.UserId;

        var access = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineSchedule>();
        }

        return await RescheduleForChildAsync(command.ChildId, command.MedicineId, userId, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);
    }

    // Shared with RescheduleMedicineForGroupHandler -- everything past authorization is
    // identical. NotFound means no matching, non-stopped schedule for this child.
    internal static async Task<Result<MedicineSchedule>> RescheduleForChildAsync(
        UserId childId, MedicineId medicineId, UserId modifiedBy, IReadOnlyList<TimeOnly> times, DateOnly startDate, DateOnly? endDate, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var events = await medicines.ReadAsync(medicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != childId)
        {
            return new Result<MedicineSchedule>.NotFound();
        }

        var before = new MedicineWindow(schedule.Times, schedule.StartDate, schedule.EndDate);
        var after = new MedicineWindow(times, startDate, endDate);

        if (before == after)
        {
            return new Result<MedicineSchedule>.Success(schedule);
        }

        await medicines.AppendAsync(medicineId, [new MedicineScheduleRescheduled(medicineId, before, after, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule with { Times = times, StartDate = startDate, EndDate = endDate, LastModifiedBy = modifiedBy });
    }
}
