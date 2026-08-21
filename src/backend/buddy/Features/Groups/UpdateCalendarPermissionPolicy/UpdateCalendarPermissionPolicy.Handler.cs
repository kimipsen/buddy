namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyHandler
{
    public static async Task<GroupAccess> Handle(UpdateCalendarPermissionPolicy command, IGroupEventStore groups, CancellationToken cancellationToken)
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

        await groups.AppendAsync(
            command.GroupId,
            [new GroupCalendarPolicyUpdated(command.GroupId, command.Policy, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return GroupAccess.Allowed;
    }
}
