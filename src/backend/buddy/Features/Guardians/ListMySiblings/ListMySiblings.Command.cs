using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Guardians;

public sealed record ListMySiblings(UserId? ChildId)
{
    public static ListMySiblings FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}
