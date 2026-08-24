namespace buddy.Features.Groups;

public enum GroupInviteStatus
{
    Pending,
    Accepted,
    Revoked
}

// Queryable read-model index kept alongside the Group event stream, the same pattern as
// GroupMembershipDocument. TokenHash is looked up directly (PreviewGroupInvite/AcceptGroupInvite
// only ever have the raw token from an email link, not the InviteId), so it's stored as a plain
// field rather than as the document Id. InvitedEmail is stored normalized (trimmed, lowercased)
// so lookups by email are a plain equality match -- see GroupEvents.cs for why this is never
// resolved to a UserId ahead of time.
public sealed record GroupInviteDocument(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string InvitedEmail,
    GroupRole Role,
    Guid InvitedBy,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    GroupInviteStatus Status)
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
