using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record CreateFeelingsShareLink(UserId UserId, UserId ChildId, DateTimeOffset? ExpiresAt)
{
    public static CreateFeelingsShareLink FromClaims(ClaimsPrincipal principal, UserId childId, DateTimeOffset? expiresAt) =>
        new(principal.GetRequiredUserId(), childId, expiresAt);
}
