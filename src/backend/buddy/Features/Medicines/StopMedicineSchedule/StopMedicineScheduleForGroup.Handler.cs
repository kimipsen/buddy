using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class StopMedicineScheduleForGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        StopMedicineScheduleForGroup command,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MedicineGroupAccess.ResolveAsync(command.GroupId, command.ChildId, command.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved;
        }

        var stopped = await StopMedicineScheduleHandler.StopForChildAsync(command.ChildId, command.MedicineId, command.UserId!, medicines, cancellationToken);

        return stopped ? new Result<Unit>.Success(Unit.Value) : new Result<Unit>.NotFound();
    }
}
