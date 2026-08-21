namespace buddy.Features.Groups;

public static class GetGroupHandler
{
    public static async Task<GetGroupResult> Handle(GetGroup query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new GetGroupResult(null, GroupAccess.NotFound);
        }

        var events = await groups.ReadAsync(query.GroupId, cancellationToken);
        var group = Group.Rehydrate(events);
        var access = GroupAuthorization.CheckView(group, userId);

        return new GetGroupResult(access == GroupAccess.Allowed ? group : null, access);
    }
}
