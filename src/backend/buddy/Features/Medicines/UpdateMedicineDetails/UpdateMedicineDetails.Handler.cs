using buddy.Common;
using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Users;

using FluentValidation;

namespace buddy.Features.Medicines;

public static class UpdateMedicineDetailsHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        UpdateMedicineDetails command,
        IValidator<UpdateMedicineDetails> validator,
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

        var result = await UpdateForChildAsync(command.ChildId, command.MedicineId, userId, command.Name, command.Dosage, command.Icon, command.Color, medicines, cancellationToken);

        return result is null ? new Result<MedicineSchedule>.NotFound() : new Result<MedicineSchedule>.Success(result);
    }

    // Shared with UpdateMedicineDetailsForGroupHandler -- everything past authorization is
    // identical, mirrors CreateMedicineScheduleHandler.CreateForChildAsync. Null means no
    // matching, non-stopped schedule for this child.
    internal static async Task<MedicineSchedule?> UpdateForChildAsync(
        UserId childId, MedicineId medicineId, UserId modifiedBy, string name, string dosage, Icon icon, Color color, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var events = await medicines.ReadAsync(medicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.IsStopped || schedule.ChildId != childId)
        {
            return null;
        }

        var before = new MedicineDetails(schedule.Name, schedule.Dosage, schedule.Icon, schedule.Color);
        var after = new MedicineDetails(name, dosage, icon, color);

        if (before == after)
        {
            return schedule;
        }

        await medicines.AppendAsync(medicineId, [new MedicineDetailsUpdated(medicineId, before, after, modifiedBy, DateTimeOffset.UtcNow)], cancellationToken);

        return schedule with { Name = name, Dosage = dosage, Icon = icon, Color = color, LastModifiedBy = modifiedBy };
    }
}
