using buddy.Common;
using buddy.Features.Guardians;

namespace buddy.Features.Groups;

public static class AddChildToGroupHandler
{
    public static async Task<Result<Unit>> Handle(
        AddChildToGroup command,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        // No relationship to the child at all -- collapsed to NotFound, the same way
        // MealplanAuthorization/MedicineAuthorization treat a non-guardian caller. A group
        // manager who isn't this child's guardian can't add them, mirroring
        // ShareMealPlanWithGroup's two-sided consent (guardian authority + group management both
        // required, checked in this order so a caller can't probe group membership for a child
        // they don't guard).
        var link = await guardians.FindActiveLinkAsync(command.ChildId, userId, cancellationToken);
        if (link is null)
        {
            return new Result<Unit>.NotFound();
        }

        var group = Group.Rehydrate(await groups.ReadAsync(command.GroupId, cancellationToken));
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        if (group!.Members.ContainsKey(command.ChildId))
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        // Always Member -- unlike SetGroupMemberRole, the role isn't caller-selectable here,
        // since a child has no business becoming a group Owner/Admin.
        await groups.AppendAsync(
            command.GroupId,
            [new GroupMemberRoleGranted(command.GroupId, command.ChildId, GroupRole.Member, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
