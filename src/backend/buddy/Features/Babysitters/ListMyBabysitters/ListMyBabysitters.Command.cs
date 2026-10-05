using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record ListMyBabysitters(UserId UserId)
{
    public static ListMyBabysitters FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
