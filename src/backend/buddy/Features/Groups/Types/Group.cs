using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Medicines;
using buddy.Features.Mealplans;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record Group(
    GroupId Id,
    string Name,
    ImmutableDictionary<UserId, GroupRole> Members,
    ImmutableDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    ImmutableDictionary<GroupRole, MealplanAccessTier> MealplanPermissionPolicy,
    ImmutableDictionary<GroupRole, MedicineAccessTier> MedicinePermissionPolicy,
    bool IsDeleted = false)
{
    public static Group? Rehydrate(IEnumerable<GroupEvent> events)
    {
        Group? group = null;

        foreach (var @event in events)
        {
            group = @event switch
            {
                // MealplanPermissionPolicy starts empty (fails closed) -- GroupCreated is an
                // already-shipped event and cannot gain a required field retroactively, the same
                // constraint CalendarPermissionPolicy would have hit if it weren't baked in from
                // day one. A newly created group gets an explicit policy via a second event
                // appended in the same transaction (see CreateGroupHandler), not from here.
                GroupCreated created => new Group(
                    created.GroupId,
                    created.Name,
                    ImmutableDictionary<UserId, GroupRole>.Empty.Add(created.OwnerId, GroupRole.Owner),
                    created.CalendarPermissionPolicy,
                    ImmutableDictionary<GroupRole, MealplanAccessTier>.Empty,
                    ImmutableDictionary<GroupRole, MedicineAccessTier>.Empty),
                GroupMemberRoleGranted granted => group! with { Members = group!.Members.SetItem(granted.MemberId, granted.Role) },
                GroupMemberRoleRevoked revoked => group! with { Members = group!.Members.Remove(revoked.MemberId) },
                GroupCalendarPolicyUpdated updated => group! with { CalendarPermissionPolicy = updated.Policy },
                GroupMealplanPolicyUpdated updated => group! with { MealplanPermissionPolicy = updated.Policy },
                GroupMedicinePolicyUpdated updated => group! with { MedicinePermissionPolicy = updated.Policy },
                GroupDeleted => group! with { IsDeleted = true },
                _ => group
            };
        }

        return group;
    }
}
