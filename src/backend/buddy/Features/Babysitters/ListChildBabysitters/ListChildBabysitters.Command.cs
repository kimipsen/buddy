using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public sealed record ListChildBabysitters(UserId UserId, UserId ChildId)
{
    public static ListChildBabysitters FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
