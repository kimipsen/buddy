using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class UpdateMedicineDetailsForGroupHandler
{
    public static async Task<Result<MedicineSchedule>> Handle(
        UpdateMedicineDetailsForGroup command,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return new Result<MedicineSchedule>.Validation("A medicine schedule requires a name.");
        }

        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, MedicineSchedule>();
        }

        var result = await UpdateMedicineDetailsHandler.UpdateForChildAsync(
            command.ChildId, command.MedicineId, command.UserId!, command.Name, command.Dosage, command.Icon, command.Color, medicines, cancellationToken);

        return result is null ? new Result<MedicineSchedule>.NotFound() : new Result<MedicineSchedule>.Success(result);
    }
}
