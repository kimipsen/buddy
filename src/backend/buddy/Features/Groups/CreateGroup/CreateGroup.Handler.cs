using System.Collections.Immutable;

using buddy.Features.Calendars;

namespace buddy.Features.Groups;

public static class CreateGroupHandler
{
    // Default policy on creation: matches the intuitive expectation out of the box, but every
    // entry -- including Owner -- can be reconfigured afterward through UpdateCalendarPermissionPolicy.
    // No role is ever implicitly elevated outside this policy.
    private static readonly ImmutableDictionary<GroupRole, CalendarRole> DefaultPolicy = ImmutableDictionary<GroupRole, CalendarRole>.Empty
        .Add(GroupRole.Owner, CalendarRole.Owner)
        .Add(GroupRole.Admin, CalendarRole.Contributor)
        .Add(GroupRole.Member, CalendarRole.Viewer);

    public static async Task<CreateGroupResult> Handle(CreateGroup command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } ownerId)
        {
            return new CreateGroupResult(null, Unauthenticated: true);
        }

        var groupId = GroupId.New();
        var created = new GroupCreated(groupId, ownerId, command.Name, DefaultPolicy, DateTimeOffset.UtcNow);

        var events = await groups.CreateAsync(groupId, [created], cancellationToken);

        return new CreateGroupResult(Group.Rehydrate(events));
    }
}
