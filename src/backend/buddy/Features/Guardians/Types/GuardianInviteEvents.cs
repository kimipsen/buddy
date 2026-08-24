using buddy.Features.Users;

namespace buddy.Features.Guardians;

// A dedicated stream for the invite's own lifecycle, unlike GroupInviteCreated/etc which append
// to the Group's own stream -- a Group already exists before anyone is invited to it, but a
// guardian invite has no pre-existing aggregate to attach to (the GuardianLink it leads to
// doesn't exist until accepted). Nothing ever rehydrates this stream back into an object, the
// same way nothing rehydrates a "GroupInvite" from the Group stream -- these events exist to
// drive the GuardianInviteDocument projection and to be part of this invite's own history.
public union GuardianInviteEvent(
    GuardianInviteCreated,
    GuardianInviteAccepted,
    GuardianInviteRevoked
)
{
    public static GuardianInviteEvent FromPayload(object payload) => payload switch
    {
        GuardianInviteCreated e => e,
        GuardianInviteAccepted e => e,
        GuardianInviteRevoked e => e,
        _ => throw new ArgumentException($"Unknown guardian invite event payload: {payload.GetType().Name}", nameof(payload)),
    };

    public string EventType => this switch
    {
        GuardianInviteCreated => nameof(GuardianInviteCreated),
        GuardianInviteAccepted => nameof(GuardianInviteAccepted),
        GuardianInviteRevoked => nameof(GuardianInviteRevoked),
    };
}

// InvitedEmail is deliberately never resolved to a UserId at invite time, for the same
// account-enumeration reason GroupInviteCreated isn't -- AcceptGuardianInvite instead compares
// the accepting caller's own verified email. TokenHash is a SHA-256 hash, never the plaintext
// token, for the same reason GroupInviteCreated's is. ChildGivenName is denormalized here (the
// handler already loads the child's User to build the invite email) so the unauthenticated
// preview endpoint can show "you're invited to help manage X's account" without a second lookup
// -- the same role GroupInviteDocument.GroupName plays for group invites.
public sealed record GuardianInviteCreated(GuardianInviteId Id, UserId ChildId, string ChildGivenName, string InvitedEmail, GuardianKind Kind, UserId InvitedBy, string TokenHash, DateTimeOffset ExpiresAt, DateTimeOffset OccurredAt);

public sealed record GuardianInviteAccepted(GuardianInviteId Id, UserId AcceptedBy, DateTimeOffset OccurredAt);

public sealed record GuardianInviteRevoked(GuardianInviteId Id, UserId RevokedBy, DateTimeOffset OccurredAt);
