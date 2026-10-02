using buddy.Common;

namespace buddy.Features.Groups;

public static class ListGroupInvitesHandler
{
    public static async Task<Result<IReadOnlyCollection<GroupInviteDocument>>> Handle(ListGroupInvites query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var events = await groups.ReadAsync(query.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);

        if (group is null)
        {
            return new Result<IReadOnlyCollection<GroupInviteDocument>>.NotFound();
        }

        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<GroupInviteDocument>>();
        }

        var invites = await groups.ListPendingInvitesAsync(query.GroupId, cancellationToken);
        return new Result<IReadOnlyCollection<GroupInviteDocument>>.Success(invites);
    }
}
