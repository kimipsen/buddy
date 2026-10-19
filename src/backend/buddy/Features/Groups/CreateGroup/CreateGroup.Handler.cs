using System.Collections.Immutable;

using buddy.Common.Validation;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Mealplans;
using buddy.Features.Medicines;
using buddy.Features.Users;

using FluentValidation;

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

    public static async Task<CreateGroupOutcome> Handle(
        CreateGroup command,
        IValidator<CreateGroup> validator,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (await validator.ValidateCommandAsync(command, cancellationToken) is { } problem)
        {
            return new CreateGroupOutcome.Validation(problem);
        }

        var ownerId = command.UserId;

        var groupId = GroupId.New();
        var now = time.GetUtcNow();
        var created = new GroupCreated(groupId, ownerId, command.Name, DefaultCalendarPolicy, DefaultMealplanPolicy, DefaultMedicinePolicy, now);

        var events = await groups.CreateAsync(groupId, [created], cancellationToken);
        var group = Group.Replay(events);
        var members = await GroupMemberResolver.ResolveAsync(group, guardians, users, cancellationToken);

        return new CreateGroupOutcome.Success(new GroupWithMemberDetails(group, members));
    }
}
