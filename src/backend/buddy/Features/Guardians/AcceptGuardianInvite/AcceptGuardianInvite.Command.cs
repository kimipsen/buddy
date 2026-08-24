using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record AcceptGuardianInvite(UserId? UserId, string Token)
{
    public static AcceptGuardianInvite FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetUserId(), token);
}
