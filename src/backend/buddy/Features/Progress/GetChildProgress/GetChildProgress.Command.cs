using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Progress;

public sealed record GetChildProgress(UserId? CallerId, UserId ChildId)
{
    public static GetChildProgress FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetUserId(), childId);
}
