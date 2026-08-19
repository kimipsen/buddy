using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record VerifyEmail(UserId? UserId, string Token)
{
    public static VerifyEmail FromClaims(ClaimsPrincipal principal, string token) => new(principal.GetUserId(), token);
}
