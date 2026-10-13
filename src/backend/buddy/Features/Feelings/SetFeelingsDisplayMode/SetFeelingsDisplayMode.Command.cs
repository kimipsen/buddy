using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record SetFeelingsDisplayMode(UserId UserId, UserId ChildId, FeelingsDisplayMode DisplayMode)
{
    public static SetFeelingsDisplayMode FromClaims(ClaimsPrincipal principal, UserId childId, FeelingsDisplayMode displayMode) =>
        new(principal.GetRequiredUserId(), childId, displayMode);
}
