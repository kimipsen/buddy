using buddy.Common;

namespace buddy.Features.Groups;

public static class UpdateCalendarPermissionPolicyHandler
{
    public static async Task<Result<Unit>> Handle(UpdateCalendarPermissionPolicy command, IGroupEventStore groups, CancellationToken cancellationToken)
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
            return access == GroupAccess.Forbidden ? new Result<Unit>.Forbidden() : new Result<Unit>.NotFound();
        }

        await groups.AppendAsync(
            command.GroupId,
            [new GroupCalendarPolicyUpdated(command.GroupId, command.Policy, userId, DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}
