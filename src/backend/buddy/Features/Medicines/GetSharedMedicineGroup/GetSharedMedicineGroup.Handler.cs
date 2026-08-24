using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

// The only read path for "is this child's medicine currently shared, and with which group" --
// gated on Manage tier (guardian only), the same principal who can share/unshare in the first
// place. Mirrors GetSharedGroupHandler.
public static class GetSharedMedicineGroupHandler
{
    public static async Task<Result<GroupId?>> Handle(
        GetSharedMedicineGroup query, IMedicineSharingEventStore sharing, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<GroupId?>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(query.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<GroupId?>();
        }

        var sharingId = await sharing.FindIdForChildAsync(query.ChildId, cancellationToken);

        if (sharingId is null)
        {
            return new Result<GroupId?>.Success(null);
        }

        var record = MedicineSharing.Rehydrate(await sharing.ReadAsync(sharingId, cancellationToken))!;

        return new Result<GroupId?>.Success(record.SharedWithGroupId);
    }
}
