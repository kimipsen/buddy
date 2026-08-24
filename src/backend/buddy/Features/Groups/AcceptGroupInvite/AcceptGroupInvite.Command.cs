using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record AcceptGroupInvite(UserId? UserId, string Token)
{
    public static AcceptGroupInvite FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetUserId(), token);
}
