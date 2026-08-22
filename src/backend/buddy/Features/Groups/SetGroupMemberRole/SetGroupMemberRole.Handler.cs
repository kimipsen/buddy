using buddy.Common;

namespace buddy.Features.Groups;

public static class SetGroupMemberRoleHandler
{
    public static async Task<Result<Unit>> Handle(SetGroupMemberRole command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.Role == GroupRole.Owner)
        {
            // Ownership is assigned only at creation and never granted through this endpoint.
            return new Result<Unit>.Forbidden();
        }

        if (command.UserId is not { } userId)
        {
            return new Result<Unit>.NotFound();
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access == GroupAccess.Forbidden ? new Result<Unit>.Forbidden() : new Result<Unit>.NotFound();
        }

        if (command.MemberId == userId)
        {
            // The owner's own role can't be changed through this endpoint either.
            return new Result<Unit>.Forbidden();
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupMemberRoleGranted(command.GroupId, command.MemberId, command.Role, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
