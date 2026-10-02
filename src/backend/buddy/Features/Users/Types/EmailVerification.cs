namespace buddy.Features.Users;

// Whether the user's current address has a verification outstanding. Pending carries the hashed
// token (see EmailVerificationRequested) and its timestamps, which are always set and cleared
// together. Persisted in the User snapshot through EmailVerificationJsonConverter. See
// docs/backend/analysis/eliminate-nulls.md, Phase 5.6.
public union EmailVerification(EmailVerification.None, EmailVerification.Pending)
{
    public sealed record None;

    public sealed record Pending(string TokenHash, DateTimeOffset RequestedAt, DateTimeOffset ExpiresAt);
}
