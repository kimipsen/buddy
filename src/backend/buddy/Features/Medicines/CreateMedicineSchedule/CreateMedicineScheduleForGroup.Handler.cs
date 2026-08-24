using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class CreateMedicineScheduleForGroupHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        CreateMedicineScheduleForGroup command,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
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

        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, MedicineSchedule>();
        }

        var schedule = await CreateMedicineScheduleHandler.CreateForChildAsync(
            command.ChildId, command.UserId!, command.Name, command.Dosage, command.Icon, command.Color, command.Times, command.StartDate, command.EndDate, medicines, cancellationToken);

        return new Result<MedicineSchedule>.Success(schedule);
    }
}
