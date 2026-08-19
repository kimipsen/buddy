using System.Security.Claims;

namespace buddy.Features.Users;

// Exactly one of AfterVersion/BeforeVersion is set by the endpoint, per the decoded cursor's
// direction. Neither set means "first page" (forward from the start of the stream).
public sealed record EventsPageRequest(long? AfterVersion, long? BeforeVersion, int PageSize)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;
}

public sealed record GetUserEvents(UserId? UserId, EventsPageRequest Page)
{
    public static GetUserEvents FromClaims(ClaimsPrincipal principal, EventsPageRequest page) =>
        new(principal.GetUserId(), page);
}
