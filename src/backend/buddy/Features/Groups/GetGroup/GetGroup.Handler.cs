using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public static class GetGroupHandler
{
    public static async Task<Result<GroupWithMemberDetails>> Handle(
        GetGroup query,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var group = await groups.FindSnapshotAsync(query.GroupId, cancellationToken);

        if (group is null)
        {
            return new Result<GroupWithMemberDetails>.NotFound();
        }

        var access = GroupAuthorization.CheckView(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<GroupWithMemberDetails>();
        }

        var members = await GroupMemberResolver.ResolveAsync(group, guardians, users, cancellationToken);

        return new Result<GroupWithMemberDetails>.Success(new GroupWithMemberDetails(group, members));
    }
}
