using buddy.Common;

namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyHandler
{
    public static async Task<Result<Unit>> Handle(UpdateCalendarPermissionPolicy command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = command.UserId;

        var events = await groups.ReadAsync(command.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);

        if (group is null)
        {
            return new Result<Unit>.NotFound();
        }

        var access = GroupAuthorization.CheckManage(group, userId);

        if (access != GroupAccess.Allowed)
        {
            return access.ToDeniedResult<Unit>();
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupCalendarPolicyUpdated(command.GroupId, command.Policy, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
