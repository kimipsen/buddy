using System.Diagnostics;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Mealplans;

// Authorizes a caller against a group's MealplanPermissionPolicy for a plan the family has
// chosen to share with that group -- entirely independent of MealplanAuthorization's
// guardian/child resolution, which this never touches (see
// docs/backend/analysis/group-owned-mealplans.md). Two meaningful tiers: View (read-only) and
// Manage (read-write) -- Rate is reserved for the child's own tier and, even though
// UpdateMealplanPermissionPolicy's validation already rejects it as a policy value, ResolveTier
// still treats it as None defensively rather than trusting that invariant blindly.
public static class MealplanGroupAuthorization
{
    public static async Task<MealplanAccess> CheckView(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(groupId, callerId, groups, cancellationToken);

        return tier == MealplanAccessTier.None ? MealplanAccess.NotFound : MealplanAccess.Allowed;
    }

    public static async Task<MealplanAccess> CheckManage(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(groupId, callerId, groups, cancellationToken);

        return tier switch
        {
            MealplanAccessTier.Manage => MealplanAccess.Allowed,
            MealplanAccessTier.View => MealplanAccess.Forbidden,
            MealplanAccessTier.None => MealplanAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized MealplanAccessTier value for group policy: {tier}."),
        };
    }

    private static async Task<MealplanAccessTier> ResolveTier(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        if (group is null || group.IsDeleted)
        {
            return MealplanAccessTier.None;
        }

        // TryGetValue, never GetValueOrDefault: MealplanAccessTier.None is enum case 0, so
        // defaulting a missing entry would happen to be safe here, but TryGetValue keeps the
        // fail-closed intent explicit and matches CalendarAuthorization's rule for group policies.
        if (!group.Members.TryGetValue(callerId, out var role) || !group.MealplanPermissionPolicy.TryGetValue(role, out var tier))
        {
            return MealplanAccessTier.None;
        }

        return tier == MealplanAccessTier.Rate ? MealplanAccessTier.None : tier;
    }
}
