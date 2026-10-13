using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.Feelings;

public sealed record RemoveFeelingCheckIn(UserId UserId, UserId ChildId, FeelingCheckInId CheckInId)
{
    public static RemoveFeelingCheckIn FromClaims(ClaimsPrincipal principal, UserId childId, FeelingCheckInId checkInId) =>
        new(principal.GetRequiredUserId(), childId, checkInId);
}
