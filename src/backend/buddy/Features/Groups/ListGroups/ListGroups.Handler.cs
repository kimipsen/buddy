namespace buddy.Features.Groups;

public static class ListGroupsHandler
{
    public static async Task<IReadOnlyCollection<GroupMembershipDocument>> Handle(ListGroups query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        return await groups.ListForUserAsync(userId, cancellationToken);
    }
}
