namespace buddy.Features.Groups;

public static class ListGroupsHandler
{
    public static async Task<IReadOnlyCollection<GroupMembershipDocument>> Handle(ListGroups query, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return [];
        }

        return await groups.ListForUserAsync(userId, cancellationToken);
    }
}
