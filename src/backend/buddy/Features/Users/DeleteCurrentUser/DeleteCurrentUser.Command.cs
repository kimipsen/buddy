using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record DeleteUser(UserId? UserId)
{
    public static DeleteUser FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}
