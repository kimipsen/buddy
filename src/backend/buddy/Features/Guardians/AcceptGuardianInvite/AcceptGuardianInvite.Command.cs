using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record AcceptGuardianInvite(UserId UserId, string Token)
{
    public static AcceptGuardianInvite FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetRequiredUserId(), token);
}

// Distinct from the shared Result<T> only for EmailNotVerified (403 email_not_verified): the
// client needs to tell "verify your email first" apart from a plain 403 for the wrong account.
public union AcceptGuardianInviteOutcome(AcceptGuardianInviteOutcome.Success, AcceptGuardianInviteOutcome.NotFound, AcceptGuardianInviteOutcome.Forbidden, EmailNotVerified)
{
    public sealed record Success;
    public sealed record NotFound;
    public sealed record Forbidden;
}
