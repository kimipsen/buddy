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
        if (query.UserId is not { } userId)
        {
            return new Result<GroupWithMemberDetails>.NotFound();
        }

        var events = await groups.ReadAsync(query.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckView(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<GroupWithMemberDetails>();
        }

        var members = await GroupMemberResolver.ResolveAsync(group!, guardians, users, cancellationToken);

        return new Result<GroupWithMemberDetails>.Success(new GroupWithMemberDetails(group!, members));
    }
}
