using buddy.Features.Calendars;
using buddy.Features.Mealplans;
using buddy.Features.Users;

using System.Collections.Immutable;

namespace buddy.Features.Groups;

public union GroupEvent(
    GroupCreated,
    GroupMemberRoleGranted,
    GroupMemberRoleRevoked,
    GroupCalendarPolicyUpdated,
    GroupMealplanPolicyUpdated,
    GroupDeleted,
    GroupInviteCreated,
    GroupInviteAccepted,
    GroupInviteRevoked
)
{
    public static GroupEvent FromPayload(object payload) => payload switch
    {
        GroupCreated e => e,
        GroupMemberRoleGranted e => e,
        GroupMemberRoleRevoked e => e,
        GroupCalendarPolicyUpdated e => e,
        GroupMealplanPolicyUpdated e => e,
        GroupDeleted e => e,
        GroupInviteCreated e => e,
        GroupInviteAccepted e => e,
        GroupInviteRevoked e => e,
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
        GroupMealplanPolicyUpdated => nameof(GroupMealplanPolicyUpdated),
        GroupDeleted => nameof(GroupDeleted),
        GroupInviteCreated => nameof(GroupInviteCreated),
        GroupInviteAccepted => nameof(GroupInviteAccepted),
        GroupInviteRevoked => nameof(GroupInviteRevoked),
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

// Full replace, same rule as GroupCalendarPolicyUpdated -- every role must be present. Only
// MealplanAccessTier.None/Manage are ever valid values here (validated at the API boundary); Rate
// is reserved for a child's own tier and is never a meaningful group-policy target (see
// docs/backend/analysis/group-owned-mealplans.md). Appended a second time, right after
// GroupCreated in the same transaction, for every newly created group -- GroupCreated itself is
// already shipped and cannot gain a required field retroactively, so a pre-existing group has no
// entry here until one is set explicitly, which fails closed rather than guessing a default.
public sealed record GroupMealplanPolicyUpdated(GroupId GroupId, ImmutableDictionary<GroupRole, MealplanAccessTier> Policy, UserId UpdatedBy, DateTimeOffset OccurredAt);

// Recorded on the group's own stream (rather than a separate invite aggregate) so an invite's
// lifecycle is part of the same history as the membership it leads to. None of the three invite
// events touch Group.Members directly -- only the GroupMemberRoleGranted appended alongside
// GroupInviteAccepted does that -- so Group.Rehydrate has no case for them and falls through to
// its default arm; they exist purely to drive the GroupInviteDocument read model.
//
// InvitedEmail is deliberately never resolved to a UserId at invite time -- this codebase has no
// "look up a user by email" capability anywhere, by design, to avoid an account-enumeration
// surface (see docs/backend/analysis/child-accounts-and-guardian-roles.md). AcceptGroupInvite
// instead compares the *authenticated caller's own* email against InvitedEmail -- a self-scoped
// check, not a lookup of someone else.
//
// TokenHash is a SHA-256 hash of the plaintext token, never the token itself -- same reasoning as
// EmailVerificationRequested: the event stream is append-only, so a bare secret in it could never
// be revoked or purged.
public sealed record GroupInviteCreated(GroupId GroupId, Guid InviteId, string InvitedEmail, GroupRole Role, UserId InvitedBy, string TokenHash, DateTimeOffset ExpiresAt, DateTimeOffset OccurredAt);

public sealed record GroupInviteAccepted(GroupId GroupId, Guid InviteId, UserId AcceptedBy, DateTimeOffset OccurredAt);

public sealed record GroupInviteRevoked(GroupId GroupId, Guid InviteId, UserId RevokedBy, DateTimeOffset OccurredAt);
