namespace buddy.Features.Groups;

public static class RemoveGroupMemberHandler
{
    public static async Task<GroupAccess> Handle(RemoveGroupMember command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } userId)
        {
            return GroupAccess.NotFound;
        }

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access;
        }

        if (command.MemberId == userId)
        {
            // The owner can't remove themselves -- deleting the group is the only way to end it.
            return GroupAccess.Forbidden;
        }

        if (!group!.Members.ContainsKey(command.MemberId))
        {
            return GroupAccess.Allowed;
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupMemberRoleRevoked(command.GroupId, command.MemberId, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return GroupAccess.Allowed;
    }
}
