using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public sealed record SharedMedicineGroup(GroupId Id, string Name);

// The only read path for "is this child's medicine currently shared, and with which group" --
// gated on Manage tier (guardian only), the same principal who can share/unshare in the first
// place. Mirrors GetSharedGroupHandler.
public static class GetSharedMedicineGroupHandler
{
    public static async Task<Result<SharedMedicineGroup?>> Handle(
        GetSharedMedicineGroup query, IMedicineSharingEventStore sharing, IGuardianLinkEventStore guardians, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<SharedMedicineGroup?>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<SharedMedicineGroup?>();
        }

        var sharingId = await sharing.FindIdForChildAsync(query.ChildId, cancellationToken);

        if (sharingId is null)
        {
            return new Result<SharedMedicineGroup?>.Success(null);
        }

        var record = MedicineSharing.Rehydrate(await sharing.ReadAsync(sharingId, cancellationToken))!;

        if (record.SharedWithGroupId is not { } groupId)
        {
            return new Result<SharedMedicineGroup?>.Success(null);
        }

        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        return new Result<SharedMedicineGroup?>.Success(group is null ? null : new SharedMedicineGroup(groupId, group.Name));
    }
}
