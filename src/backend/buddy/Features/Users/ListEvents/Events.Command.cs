using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record GetUserEvents(KeycloakSubject Subject, long AfterVersion, int PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static GetUserEvents FromClaims(ClaimsPrincipal principal, long afterVersion, int pageSize) =>
        new(principal.GetKeycloakSubject(), afterVersion, pageSize);
}
