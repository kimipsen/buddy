using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// Revision is the board the guardian was shown (0 for a child's first board); the new board gets
// Revision + 1.
public sealed record SetFirstThenBoard(UserId UserId, UserId ChildId, int Revision, FirstThenCard First, FirstThenCard Then, int? ThenMinutes)
{
    public static SetFirstThenBoard FromClaims(ClaimsPrincipal principal, UserId childId, SetFirstThenBoardRequest request) =>
        new(principal.GetRequiredUserId(), childId, request.Revision, request.First.ToCard(), request.Then.ToCard(), request.ThenMinutes);
}
