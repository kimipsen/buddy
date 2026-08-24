using buddy.Common;

namespace buddy.Features.Groups;

public static class RevokeGroupInviteHandler
{
    public static async Task<Result<Unit>> Handle(RevokeGroupInvite command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        var invite = await groups.FindInviteAsync(command.InviteId, cancellationToken);

        if (invite is null)
        {
            // Nothing to revoke -- same idempotent-delete convention as RemoveGroupMember.
            return new Result<Unit>.Success(Unit.Value);
        }

        if (invite.GroupId != command.GroupId.Value)
        {
            // Belongs to a different group -- don't confirm or deny it exists elsewhere.
            return new Result<Unit>.NotFound();
        }

        if (invite.Status != GroupInviteStatus.Pending)
        {
            return new Result<Unit>.Success(Unit.Value);
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupInviteRevoked(command.GroupId, invite.Id, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
