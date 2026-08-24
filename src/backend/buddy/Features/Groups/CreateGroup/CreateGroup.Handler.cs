using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Mealplans;

namespace buddy.Features.Groups;

public static class CreateGroupHandler
{
    // Default policy on creation: matches the intuitive expectation out of the box, but every
    // entry -- including Owner -- can be reconfigured afterward through UpdateCalendarPermissionPolicy.
    // No role is ever implicitly elevated outside this policy.
    private static readonly ImmutableDictionary<GroupRole, CalendarRole> DefaultCalendarPolicy = ImmutableDictionary<GroupRole, CalendarRole>.Empty
        .Add(GroupRole.Owner, CalendarRole.Owner)
        .Add(GroupRole.Admin, CalendarRole.Contributor)
        .Add(GroupRole.Member, CalendarRole.Viewer);

    // More conservative than the calendar default: meal-plan data can include a child's personal
    // ratings/notes, so a regular Member gets no access until an Owner/Admin deliberately opts
    // them in via UpdateMealplanPermissionPolicy (see docs/backend/analysis/group-owned-mealplans.md).
    private static readonly ImmutableDictionary<GroupRole, MealplanAccessTier> DefaultMealplanPolicy = ImmutableDictionary<GroupRole, MealplanAccessTier>.Empty
        .Add(GroupRole.Owner, MealplanAccessTier.Manage)
        .Add(GroupRole.Admin, MealplanAccessTier.Manage)
        .Add(GroupRole.Member, MealplanAccessTier.None);

    public static async Task<CreateGroupOutcome> Handle(CreateGroup command, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (command.UserId is not { } ownerId)
        {
            return new CreateGroupOutcome.Unauthenticated();
        }

        var groupId = GroupId.New();
        var now = DateTimeOffset.UtcNow;
        var created = new GroupCreated(groupId, ownerId, command.Name, DefaultCalendarPolicy, now);
        // GroupCreated can't carry MealplanPermissionPolicy directly -- it already shipped before
        // this policy existed -- so every newly created group gets an explicit default via a
        // second event, appended in the same transaction.
        var mealplanPolicySet = new GroupMealplanPolicyUpdated(groupId, DefaultMealplanPolicy, ownerId, now);

        var events = await groups.CreateAsync(groupId, [created, mealplanPolicySet], cancellationToken);

        return new CreateGroupOutcome.Success(Group.Rehydrate(events)!);
    }
}
