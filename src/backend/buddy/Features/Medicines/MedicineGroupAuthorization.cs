using System.Diagnostics;

using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Medicines;

// Authorizes a caller against a group's MedicinePermissionPolicy, entirely independent of
// MedicineAuthorization's guardian/child resolution, which this never touches -- mirrors
// MealplanGroupAuthorization. Two meaningful tiers: None and Manage -- Mark is reserved for the
// child/guardian's own tier and, even though UpdateMedicinePermissionPolicy's validation already
// rejects it as a policy value, ResolveTier still treats it as None defensively rather than
// trusting that invariant blindly.
public static class MedicineGroupAuthorization
{
    public static async Task<MedicineAccess> CheckManage(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(groupId, callerId, groups, cancellationToken);

        return tier switch
        {
            MedicineAccessTier.Manage => MedicineAccess.Allowed,
            MedicineAccessTier.None => MedicineAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized MedicineAccessTier value for group policy: {tier}."),
        };
    }

    private static async Task<MedicineAccessTier> ResolveTier(GroupId groupId, UserId callerId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

        if (group is null || group.IsDeleted)
        {
            return MedicineAccessTier.None;
        }

        // TryGetValue, never GetValueOrDefault: fails closed the same way
        // MealplanGroupAuthorization/CalendarAuthorization do for a group policy.
        if (!group.Members.TryGetValue(callerId, out var role) || !group.MedicinePermissionPolicy.TryGetValue(role, out var tier))
        {
            return MedicineAccessTier.None;
        }

        return tier == MedicineAccessTier.Mark ? MedicineAccessTier.None : tier;
    }
}
