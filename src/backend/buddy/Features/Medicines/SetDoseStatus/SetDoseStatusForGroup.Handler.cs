using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class SetDoseStatusForGroupHandler
{
    public static async Task<Result<MedicineDoseOccurrence>> Handle(
        SetDoseStatusForGroup command,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, MedicineDoseOccurrence>();
        }

        return await SetDoseStatusHandler.SetForChildAsync(command.ChildId, command.MedicineId, command.Date, command.Time, command.Status, command.UserId!, medicines, cancellationToken);
    }
}
