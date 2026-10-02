using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public sealed record ReplaceWorkPattern(UserId UserId, WorkPattern Pattern)
{
    public static ReplaceWorkPattern FromClaims(ClaimsPrincipal principal, WorkPattern pattern) =>
        new(principal.GetRequiredUserId(), pattern);
}
