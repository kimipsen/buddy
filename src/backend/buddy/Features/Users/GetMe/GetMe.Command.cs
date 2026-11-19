using System.Security.Claims;

namespace buddy.Features.Users;

// The caller's own profile, read only: unlike GetOrCreateUser it never provisions. Used by the MCP
// tools whoami and get_today, which run after the provisioning middleware has already vouched for
// the caller.
public sealed record GetMe(UserId UserId)
{
    public static GetMe FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
