using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Resolves whether a group-keyed request may operate on a specific child's medicine schedules.
// Unlike MealplanGroupAccess, there's no single anchor child to resolve from a group alone -- a
// group can have several children's medicine independently shared with it, so the caller always
// names the child in the route, and this only confirms two things: the caller's tier in the
// group's MedicinePermissionPolicy, and that this exact child is currently shared with this exact
// group. There's also only one tier worth resolving to (Manage) -- MedicinePermissionPolicy never
// holds a read-only value, so group access is all-or-nothing, unlike the View/Manage split
// MealplanGroupAccess has.
public static class MedicineGroupAccess
{
    public static async Task<Result<Unit>> ResolveAsync(
        GroupId groupId,
        UserId childId,
        UserId? callerId,
        IGroupEventStore groups,
        IMedicineSharingEventStore sharing,
        CancellationToken cancellationToken)
    {
        if (callerId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await MedicineGroupAuthorization.CheckManage(groupId, userId, groups, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var sharingId = await sharing.FindIdForChildAsync(childId, cancellationToken);

        if (sharingId is null)
        {
            return new Result<Unit>.NotFound();
        }

        var record = MedicineSharing.Rehydrate(await sharing.ReadAsync(sharingId, cancellationToken));

        if (record?.SharedWithGroupId != groupId)
        {
            return new Result<Unit>.NotFound();
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
