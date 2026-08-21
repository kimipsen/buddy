using System.Collections.Immutable;

using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record Group(
    GroupId Id,
    string Name,
    ImmutableDictionary<UserId, GroupRole> Members,
    ImmutableDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy,
    bool IsDeleted = false)
{
    public static Group? Rehydrate(IEnumerable<GroupEvent> events)
    {
        Group? group = null;

        foreach (var @event in events)
        {
            group = @event switch
            {
                GroupCreated created => new Group(
                    created.GroupId,
                    created.Name,
                    ImmutableDictionary<UserId, GroupRole>.Empty.Add(created.OwnerId, GroupRole.Owner),
                    created.CalendarPermissionPolicy),
                GroupMemberRoleGranted granted => group! with { Members = group!.Members.SetItem(granted.MemberId, granted.Role) },
                GroupMemberRoleRevoked revoked => group! with { Members = group!.Members.Remove(revoked.MemberId) },
                GroupCalendarPolicyUpdated updated => group! with { CalendarPermissionPolicy = updated.Policy },
                GroupDeleted => group! with { IsDeleted = true },
                _ => group
            };
        }

        return group;
    }
}
