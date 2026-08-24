using buddy.Common;

namespace buddy.Features.Groups;

public static class UpdateMealplanPermissionPolicyHandler
{
    public static async Task<Result<Unit>> Handle(UpdateMealplanPermissionPolicy command, IGroupEventStore groups, CancellationToken cancellationToken)
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

        await groups.AppendAsync(
            command.GroupId,
            [new GroupMealplanPolicyUpdated(command.GroupId, command.Policy, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
