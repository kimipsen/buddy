using buddy.Common;
using buddy.Features.Groups;

namespace buddy.Features.Medicines;

public static class ListMedicineSchedulesForGroupHandler
{
    public static async Task<Result<IReadOnlyCollection<MedicineSchedule>>> Handle(
        ListMedicineSchedulesForGroup query,
        IMedicineEventStore medicines,
        IMedicineSharingEventStore sharing,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var resolved = await MedicineGroupAccess.ResolveAsync(query.GroupId, query.ChildId, query.UserId, groups, sharing, cancellationToken);

        if (resolved is not Result<Unit>.Success)
        {
            return resolved.Reraise<Unit, IReadOnlyCollection<MedicineSchedule>>();
        }

        var loaded = await ListMedicineSchedulesHandler.ListForChildAsync(query.ChildId, medicines, cancellationToken);

        return new Result<IReadOnlyCollection<MedicineSchedule>>.Success(loaded);
    }
}
