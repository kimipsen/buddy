using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateName(UserId? UserId, Name Name)
{
    public static UpdateName FromClaims(ClaimsPrincipal principal, Name name) => new(principal.GetUserId(), name);
}
