namespace buddy.Features.Guardians;

public enum GuardianInviteStatus
{
    Pending,
    Accepted,
    Revoked
}

// Queryable read-model index for the invite's own stream, the same pattern GroupInviteDocument
// is for the Group stream. TokenHash is looked up directly (PreviewGuardianInvite/
// AcceptGuardianInvite only ever have the raw token from an email link), so it's a plain field
// rather than the document Id. InvitedEmail is stored normalized (trimmed, lowercased).
public sealed record GuardianInviteDocument(
    Guid Id,
    Guid ChildId,
    string ChildGivenName,
    string InvitedEmail,
    GuardianKind Kind,
    Guid InvitedBy,
    string TokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    GuardianInviteStatus Status)
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
