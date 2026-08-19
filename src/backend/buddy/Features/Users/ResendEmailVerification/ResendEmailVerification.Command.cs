using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record ResendEmailVerification(UserId? UserId)
{
    public static ResendEmailVerification FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}
