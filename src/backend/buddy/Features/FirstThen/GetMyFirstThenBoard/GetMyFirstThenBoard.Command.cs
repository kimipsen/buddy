using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FirstThen;

public sealed record GetMyFirstThenBoard(UserId UserId)
{
    public static GetMyFirstThenBoard FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
