using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public sealed record GetMyTransitionWarnings(UserId UserId)
{
    public static GetMyTransitionWarnings FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
