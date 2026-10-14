using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// ChildId is null on the child's own /me route. Revision is the board the caller was shown.
public sealed record ReopenFirstThenFirst(UserId UserId, UserId? ChildId, int Revision)
{
    public static ReopenFirstThenFirst FromClaims(ClaimsPrincipal principal, UserId? childId, FirstThenRevisionRequest request) =>
        new(principal.GetRequiredUserId(), childId, request.Revision);
}
