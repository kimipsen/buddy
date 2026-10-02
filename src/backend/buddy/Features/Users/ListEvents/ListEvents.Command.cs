using System.Security.Claims;

namespace buddy.Features.Users;

public sealed record EventsPageRequest(DecodedCursor Position, int PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

public sealed record GetUserEvents(UserId UserId, EventsPageRequest Page)
{
    public static GetUserEvents FromClaims(ClaimsPrincipal principal, EventsPageRequest page) =>
        new(principal.GetRequiredUserId(), page);
}
