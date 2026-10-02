using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;

namespace buddy.Features.Medicines;

public static class ShareMedicineWithGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        ShareMedicineWithGroup command,
        IMedicineSharingEventStore sharing,
        IGuardianLinkEventStore guardians,
        IGroupEventStore groups,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var medicineAccess = await MedicineAuthorization.CheckManage(command.ChildId, userId, guardians, cancellationToken);

        if (medicineAccess != MedicineAccess.Allowed)
        {
            return medicineAccess.ToDeniedResult<Unit>();
        }

        // Sharing is a two-sided decision: the guardian and the group's own management both have
        // to consent, mirroring ShareMealPlanWithGroup.
        var group = Group.Rehydrate(await groups.ReadAsync(command.GroupId, cancellationToken));

        if (group is null)
        {
            return new Result<Unit>.NotFound();
        }

        var groupAccess = GroupAuthorization.CheckManage(group, userId);

        if (groupAccess != GroupAccess.Allowed)
        {
            return groupAccess.ToDeniedResult<Unit>();
        }

        var now = DateTimeOffset.UtcNow;
        var sharingId = await sharing.FindIdForChildAsync(command.ChildId, cancellationToken);

        if (sharingId is null)
        {
            var newId = MedicineSharingId.New();

            await sharing.CreateAsync(newId, [new MedicineSharedWithGroup(newId, command.ChildId, command.GroupId, userId, now)], cancellationToken);
        }
        else
        {
            await sharing.AppendAsync(sharingId, [new MedicineSharedWithGroup(sharingId, command.ChildId, command.GroupId, userId, now)], cancellationToken);
        }

        return new Result<Unit>.Success(Unit.Value);
    }
}
