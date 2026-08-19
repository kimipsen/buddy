using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateEmail(UserId? UserId, string Value)
{
    public static UpdateEmail FromClaims(ClaimsPrincipal principal, string value) => new(principal.GetUserId(), value);
}
