using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Authorizes a caller against a group's MealplanPermissionPolicy for a plan the family has
// chosen to share with that group -- entirely independent of MealplanAuthorization's
// guardian/child resolution, which this never touches (see
// docs/backend/analysis/group-owned-mealplans.md). Unlike CalendarAuthorization's three tiers,
// there is only one meaningful outcome here: Manage, or nothing -- Rate is reserved for the
// child's own tier and is never a valid group-policy value, so there is no partial/"Forbidden"
// state to represent; a group member either has Manage-tier access or no relationship at all.
public static class MealplanGroupAuthorization
{
    public static async Task<MealplanAccess> CheckManage(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        if (group is null || group.IsDeleted)
        {
            return MealplanAccess.NotFound;
        }

        // TryGetValue, never GetValueOrDefault: MealplanAccessTier.None is enum case 0, so
        // defaulting a missing entry would happen to be safe here, but TryGetValue keeps the
        // fail-closed intent explicit and matches CalendarAuthorization's rule for group policies.
        if (!group.Members.TryGetValue(callerId, out var role)
            || !group.MealplanPermissionPolicy.TryGetValue(role, out var tier)
            || tier != MealplanAccessTier.Manage)
        {
            return MealplanAccess.NotFound;
        }

        return MealplanAccess.Allowed;
    }
}
