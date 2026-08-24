using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record UpdateLanguage(UserId? UserId, Language Language)
{
    public static UpdateLanguage FromClaims(ClaimsPrincipal principal, Language language) => new(principal.GetUserId(), language);
}
