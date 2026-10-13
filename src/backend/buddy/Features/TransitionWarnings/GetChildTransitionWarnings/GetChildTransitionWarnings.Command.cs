using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

public sealed record GetChildTransitionWarnings(UserId UserId, UserId ChildId)
{
    public static GetChildTransitionWarnings FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}
