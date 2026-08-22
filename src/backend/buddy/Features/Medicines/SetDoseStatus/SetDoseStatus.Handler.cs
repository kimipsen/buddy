using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class SetDoseStatusHandler
{
    public static async Task<Result<MedicineDoseOccurrence>> Handle(
        SetDoseStatus command,
        IMedicineEventStore medicines,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<MedicineDoseOccurrence>.NotFound();
        }

        var access = await MedicineAuthorization.CheckMark(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineDoseOccurrence>();
        }

        var events = await medicines.ReadAsync(command.MedicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.ChildId != command.ChildId)
        {
            return new Result<MedicineDoseOccurrence>.NotFound();
        }

        if (!schedule.Times.Contains(command.Time))
        {
            return new Result<MedicineDoseOccurrence>.Validation("This medicine has no dose scheduled at that time.");
        }

        var before = schedule.DoseLog.GetValueOrDefault((command.Date, command.Time), DoseStatus.Pending);

        if (before != command.Status)
        {
            await medicines.AppendAsync(
                command.MedicineId,
                [new DoseStatusChanged(command.MedicineId, command.Date, command.Time, before, command.Status, userId, DateTimeOffset.UtcNow)],
                cancellationToken);
        }

        return new Result<MedicineDoseOccurrence>.Success(new MedicineDoseOccurrence(
            schedule.Id, schedule.Name, schedule.Dosage, schedule.Icon.Value, schedule.Color.Value,
            command.Date, command.Time, command.Status));
    }
}
