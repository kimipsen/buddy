using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// A full overwrite of a guardian-logged check-in; the moment is required, so an edit never moves
// a check-in to "now" by accident.
public sealed record EditFeelingCheckIn(
    UserId UserId,
    UserId ChildId,
    FeelingCheckInId CheckInId,
    FeelingZone Zone,
    IReadOnlyList<FeelingTag> Tags,
    string Note,
    DateOnly Date,
    TimeOnly Time) : IFeelingCheckInContent
{
    public static EditFeelingCheckIn FromClaims(ClaimsPrincipal principal, UserId childId, FeelingCheckInId checkInId, EditFeelingCheckInRequest request) =>
        new(principal.GetRequiredUserId(), childId, checkInId, request.Zone, request.Tags ?? [], FreeText.Normalize(request.Note), request.Date, request.Time);
}
