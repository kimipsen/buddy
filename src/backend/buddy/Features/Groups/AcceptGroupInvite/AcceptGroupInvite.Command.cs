using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record AcceptGroupInvite(UserId UserId, string Token)
{
    public static AcceptGroupInvite FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetRequiredUserId(), token);
}

// Distinct from the shared Result<T> only for EmailNotVerified (403 email_not_verified): the
// client needs to tell "verify your email first" apart from a plain 403 for the wrong account.
public union AcceptGroupInviteOutcome(AcceptGroupInviteOutcome.Success, AcceptGroupInviteOutcome.NotFound, AcceptGroupInviteOutcome.Forbidden, EmailNotVerified)
{
    public sealed record Success;
    public sealed record NotFound;
    public sealed record Forbidden;
}
