using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record GetMyFeelingsToday(UserId UserId)
{
    public static GetMyFeelingsToday FromClaims(ClaimsPrincipal principal) => new(principal.GetRequiredUserId());
}
