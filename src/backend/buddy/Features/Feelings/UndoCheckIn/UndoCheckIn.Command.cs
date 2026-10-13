using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record UndoCheckIn(UserId UserId, FeelingCheckInId CheckInId)
{
    public static UndoCheckIn FromClaims(ClaimsPrincipal principal, FeelingCheckInId checkInId) =>
        new(principal.GetRequiredUserId(), checkInId);
}
