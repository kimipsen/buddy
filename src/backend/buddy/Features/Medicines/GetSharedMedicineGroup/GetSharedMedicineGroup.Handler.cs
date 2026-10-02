using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

// Whether the medicine is shared with a group right now. A group that no longer exists counts as not
// shared.
public union MedicineGroupShare(MedicineGroupShare.NotShared, MedicineGroupShare.Shared)
{
    public sealed record NotShared;

    public sealed record Shared(GroupId Id, string Name);
}

// The only read path for "is this child's medicine currently shared, and with which group" --
// gated on Manage tier (guardian only), the same principal who can share/unshare in the first
// place. Mirrors GetSharedGroupHandler.
public static class GetSharedMedicineGroupHandler
{
    public static async Task<Result<MedicineGroupShare>> Handle(
        GetSharedMedicineGroup query, IMedicineSharingEventStore sharing, IGuardianLinkEventStore guardians, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var access = await MedicineAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<MedicineGroupShare>();
        }

        var sharingId = await sharing.FindIdForChildAsync(query.ChildId, cancellationToken);

        if (sharingId is null)
        {
            return new Result<MedicineGroupShare>.Success(new MedicineGroupShare.NotShared());
        }

        var record = await sharing.FindSnapshotAsync(sharingId, cancellationToken)
            ?? throw new InvalidOperationException($"No snapshot for sharingId {sharingId}, although its index says the stream exists.");

        if (record.SharedWithGroupId is not { } groupId)
        {
            return new Result<MedicineGroupShare>.Success(new MedicineGroupShare.NotShared());
        }

        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        return new Result<MedicineGroupShare>.Success(group is null
            ? new MedicineGroupShare.NotShared()
            : new MedicineGroupShare.Shared(groupId, group.Name));
    }
}
