using buddy.Features.Calendars;
using buddy.Features.Users;

using System.Collections.Immutable;

namespace buddy.Features.Groups;

public union GroupEvent(
    GroupCreated,
    GroupMemberRoleGranted,
    GroupMemberRoleRevoked,
    GroupCalendarPolicyUpdated,
    GroupDeleted
)
{
    public static GroupEvent FromPayload(object payload) => payload switch
    {
        GroupCreated e => e,
        GroupMemberRoleGranted e => e,
        GroupMemberRoleRevoked e => e,
        GroupCalendarPolicyUpdated e => e,
        GroupDeleted e => e,
        _ => throw new ArgumentException($"Unknown group event payload: {payload.GetType().Name}", nameof(payload)),
    };

    // Persistence/API discriminator. A union is a value type, so GetType().Name on a boxed
    // GroupEvent returns "GroupEvent" for every case -- use this instead.
    public string EventType => this switch
    {
        GroupCreated => nameof(GroupCreated),
        GroupMemberRoleGranted => nameof(GroupMemberRoleGranted),
        GroupMemberRoleRevoked => nameof(GroupMemberRoleRevoked),
        GroupCalendarPolicyUpdated => nameof(GroupCalendarPolicyUpdated),
        GroupDeleted => nameof(GroupDeleted),
    };
}

// CalendarPermissionPolicy is baked in at creation time (not recomputed from a hardcoded default
// on every rehydrate), so an existing group's default can never silently drift if the default
// used by CreateGroupHandler changes later.
public sealed record GroupCreated(GroupId GroupId, UserId OwnerId, string Name, ImmutableDictionary<GroupRole, CalendarRole> CalendarPermissionPolicy, DateTimeOffset OccurredAt);

public sealed record GroupDeleted(GroupId GroupId, UserId DeletedBy, DateTimeOffset OccurredAt);

// Role is always Admin or Member -- Owner is assigned only by GroupCreated.
public sealed record GroupMemberRoleGranted(GroupId GroupId, UserId MemberId, GroupRole Role, UserId GrantedBy, DateTimeOffset OccurredAt);

public sealed record GroupMemberRoleRevoked(GroupId GroupId, UserId MemberId, UserId RevokedBy, DateTimeOffset OccurredAt);

// Full replace, not a partial patch -- every role must be present for CalendarAuthorization's
// resolution to have something to look up (a role missing here fails closed, never open).
public sealed record GroupCalendarPolicyUpdated(GroupId GroupId, ImmutableDictionary<GroupRole, CalendarRole> Policy, UserId UpdatedBy, DateTimeOffset OccurredAt);
