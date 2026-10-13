using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record RevokeFeelingsShareLink(UserId UserId, UserId ChildId, FeelingsShareTokenId ShareLinkId)
{
    public static RevokeFeelingsShareLink FromClaims(ClaimsPrincipal principal, UserId childId, FeelingsShareTokenId shareLinkId) =>
        new(principal.GetRequiredUserId(), childId, shareLinkId);
}
