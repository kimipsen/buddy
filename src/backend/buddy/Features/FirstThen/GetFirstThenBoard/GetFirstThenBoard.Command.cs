using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FirstThen;

public sealed record GetFirstThenBoard(UserId UserId, UserId ChildId)
{
    public static GetFirstThenBoard FromClaims(ClaimsPrincipal principal, UserId childId) => new(principal.GetRequiredUserId(), childId);
}
