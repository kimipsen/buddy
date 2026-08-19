using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

namespace buddy.Features.Users;

// Resolves the authenticated Keycloak subject to a backend UserId once per request and stamps it
// onto the principal as a claim, so handlers can read command.UserId directly instead of each
// calling IUserEventStore.FindUserIdAsync themselves.
public sealed class UserIdClaimsTransformation(IUserEventStore users) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not { IsAuthenticated: true } || principal.HasClaim(c => c.Type == Claims.UserId))
        {
            return principal;
        }

        var userId = await users.FindUserIdAsync(principal.GetKeycloakSubject(), CancellationToken.None);

        if (userId is null)
        {
            return principal;
        }

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(Claims.UserId, userId.Value.ToString()));
        principal.AddIdentity(identity);

        return principal;
    }
}
