using buddy.Common;

namespace buddy.Features.Groups;

public static class GetGroupHandler
{
    public static async Task<Result<Group>> Handle(GetGroup query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<Group>.NotFound();
        }

        var events = await groups.ReadAsync(query.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckView(group, userId);

        return access == GroupAccess.Allowed ? new Result<Group>.Success(group!) : new Result<Group>.NotFound();
    }
}
