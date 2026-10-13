using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// The child's own check-in, now. Only ever about the caller: the /me route has no child id.
public sealed record CheckIn(UserId UserId, FeelingZone Zone, IReadOnlyList<FeelingTag> Tags, string Note) : IFeelingCheckInContent
{
    public static CheckIn FromClaims(ClaimsPrincipal principal, CheckInRequest request) =>
        new(principal.GetRequiredUserId(), request.Zone, request.Tags ?? [], FreeText.Normalize(request.Note));
}
