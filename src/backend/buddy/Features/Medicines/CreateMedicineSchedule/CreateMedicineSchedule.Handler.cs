using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        CreateMedicineSchedule command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires a name.");
        }

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

        var medicineId = MedicineId.New();
        var now = DateTimeOffset.UtcNow;

        var created = new MedicineScheduleCreated(
            medicineId, command.ChildId, userId, command.Name, command.Dosage, command.Icon, command.Color,
            command.Times, command.StartDate, command.EndDate, now);

        var events = await medicines.CreateAsync(medicineId, [created], cancellationToken);

        return new Result<MedicineSchedule>.Success(MedicineSchedule.Rehydrate(events)!);
    }
}
