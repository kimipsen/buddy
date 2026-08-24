using buddy.Common;

namespace buddy.Features.Groups;

public static class ListGroupInvitesHandler
{
    public static async Task<Result<IReadOnlyCollection<GroupInviteDocument>>> Handle(ListGroupInvites query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<GroupInviteDocument>>.NotFound();
        }

        var events = await groups.ReadAsync(query.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<GroupInviteDocument>>();
        }

        var invites = await groups.ListPendingInvitesAsync(query.GroupId, cancellationToken);
        return new Result<IReadOnlyCollection<GroupInviteDocument>>.Success(invites);
    }
}
