using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record ListFeelingsShareLinks(UserId UserId, UserId ChildId)
{
    public static ListFeelingsShareLinks FromClaims(ClaimsPrincipal principal, UserId childId) =>
        new(principal.GetRequiredUserId(), childId);
}
