using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record GetOnboardingProgress(UserId UserId)
{
    public static GetOnboardingProgress FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
