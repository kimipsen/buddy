using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateCallingName(UserId UserId, string CallingName)
{
    public static UpdateCallingName FromClaims(ClaimsPrincipal principal, string callingName) => new(principal.GetRequiredUserId(), callingName);
}
