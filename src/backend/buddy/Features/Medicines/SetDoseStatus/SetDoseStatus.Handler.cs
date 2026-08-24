using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

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

        return await SetForChildAsync(command.ChildId, command.MedicineId, command.Date, command.Time, command.Status, userId, medicines, cancellationToken);
    }

    // Shared with SetDoseStatusForGroupHandler -- everything past authorization is identical.
    internal static async Task<Result<MedicineDoseOccurrence>> SetForChildAsync(
        UserId childId, MedicineId medicineId, DateOnly date, TimeOnly time, DoseStatus status, UserId modifiedBy, IMedicineEventStore medicines, CancellationToken cancellationToken)
    {
        var events = await medicines.ReadAsync(medicineId, cancellationToken);
        var schedule = MedicineSchedule.Rehydrate(events);

        if (schedule is null || schedule.ChildId != childId)
        {
            return new Result<MedicineDoseOccurrence>.NotFound();
        }

        if (!schedule.Times.Contains(time))
        {
            return new Result<MedicineDoseOccurrence>.Validation("This medicine has no dose scheduled at that time.");
        }

        var before = schedule.DoseLog.GetValueOrDefault((date, time), DoseStatus.Pending);

        if (before != status)
        {
            await medicines.AppendAsync(
                medicineId,
                [new DoseStatusChanged(medicineId, date, time, before, status, modifiedBy, DateTimeOffset.UtcNow)],
                cancellationToken);
        }

        return new Result<MedicineDoseOccurrence>.Success(new MedicineDoseOccurrence(
            schedule.Id, schedule.Name, schedule.Dosage, schedule.Icon.Value, schedule.Color.Value,
            date, time, status));
    }
}
