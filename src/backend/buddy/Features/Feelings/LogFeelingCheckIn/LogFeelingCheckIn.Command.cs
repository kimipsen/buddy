using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// A guardian's observation on the child's behalf. Date/Time: the moment described on the child's
// wall clock, both or neither; neither means now.
public sealed record LogFeelingCheckIn(
    UserId UserId,
    UserId ChildId,
    FeelingZone Zone,
    IReadOnlyList<FeelingTag> Tags,
    string Note,
    DateOnly? Date,
    TimeOnly? Time) : IFeelingCheckInContent
{
    public static LogFeelingCheckIn FromClaims(ClaimsPrincipal principal, UserId childId, LogFeelingCheckInRequest request) =>
        new(principal.GetRequiredUserId(), childId, request.Zone, request.Tags ?? [], FreeText.Normalize(request.Note), request.Date, request.Time);
}
