using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record GetMyProgress(UserId? ChildId)
{
    public static GetMyProgress FromClaims(ClaimsPrincipal principal) => new(principal.GetUserId());
}
