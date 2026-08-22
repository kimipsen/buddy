using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class UpdateMedicineDetailsHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        UpdateMedicineDetails command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires a name.");
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

        var before = new MedicineDetails(schedule.Name, schedule.Dosage, schedule.Icon, schedule.Color);
        var after = new MedicineDetails(command.Name, command.Dosage, command.Icon, command.Color);

        if (before == after)
        {
            return new Result<MedicineSchedule>.Success(schedule);
        }

        await medicines.AppendAsync(command.MedicineId, [new MedicineDetailsUpdated(command.MedicineId, before, after, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule with { Name = command.Name, Dosage = command.Dosage, Icon = command.Icon, Color = command.Color, LastModifiedBy = userId });
    }
}
