using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

// Deliberately asymmetric with ShareMedicineWithGroupHandler: granting access needs both the
// guardian's and the group's consent, but revoking only needs the guardian's -- mirrors
// UnshareMealPlanFromGroupHandler.
public static class UnshareMedicineFromGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        UnshareMedicineFromGroup command,
        IMedicineSharingEventStore sharing,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var access = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (access != MedicineAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var sharingId = await sharing.FindIdForChildAsync(command.ChildId, cancellationToken);

        // No sharing stream yet, or not currently shared with this exact group -- unsharing is
        // idempotent, same rationale as UnshareMealPlanFromGroupHandler's.
        if (sharingId is null)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        var record = MedicineSharing.Rehydrate(await sharing.ReadAsync(sharingId, cancellationToken));

        if (record?.SharedWithGroupId != command.GroupId)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await sharing.AppendAsync(sharingId, [new MedicineUnsharedFromGroup(sharingId, command.GroupId, userId, DateTimeOffset.UtcNow)], cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
