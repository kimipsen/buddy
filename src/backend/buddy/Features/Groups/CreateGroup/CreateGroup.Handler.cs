using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Medicines;
using buddy.Features.Mealplans;
using buddy.Features.Users;

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

    // Same conservative shape as DefaultMealplanPolicy, for the same reason: medicine dosage and
    // adherence data is at least as sensitive as a child's meal ratings, so a regular Member gets
    // no access until an Owner/Admin deliberately opts them in.
    private static readonly ImmutableDictionary<GroupRole, MedicineAccessTier> DefaultMedicinePolicy = ImmutableDictionary<GroupRole, MedicineAccessTier>.Empty
        .Add(GroupRole.Owner, MedicineAccessTier.Manage)
        .Add(GroupRole.Admin, MedicineAccessTier.Manage)
        .Add(GroupRole.Member, MedicineAccessTier.None);

    // Creating a group can't fail for a provisioned caller, so this returns the group directly.
    public static async Task<GroupWithMemberDetails> Handle(
        CreateGroup command,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var ownerId = command.UserId;

        var groupId = GroupId.New();
        var now = DateTimeOffset.UtcNow;
        var created = new GroupCreated(groupId, ownerId, command.Name, DefaultCalendarPolicy, now);
        // GroupCreated can't carry MealplanPermissionPolicy/MedicinePermissionPolicy directly --
        // it already shipped before either policy existed -- so every newly created group gets
        // explicit defaults via two more events, appended in the same transaction.
        var mealplanPolicySet = new GroupMealplanPolicyUpdated(groupId, DefaultMealplanPolicy, ownerId, now);
        var medicinePolicySet = new GroupMedicinePolicyUpdated(groupId, DefaultMedicinePolicy, ownerId, now);

        var events = await groups.CreateAsync(groupId, [created, mealplanPolicySet, medicinePolicySet], cancellationToken);
        var group = Group.Replay(events);
        var members = await GroupMemberResolver.ResolveAsync(group, guardians, users, cancellationToken);

        return new GroupWithMemberDetails(group, members);
    }
}
