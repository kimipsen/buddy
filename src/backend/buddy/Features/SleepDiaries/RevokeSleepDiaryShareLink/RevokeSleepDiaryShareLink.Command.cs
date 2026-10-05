using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.SleepDiaries;

public sealed record RevokeSleepDiaryShareLink(UserId UserId, UserId ChildId, SleepDiaryShareTokenId ShareLinkId)
{
    public static RevokeSleepDiaryShareLink FromClaims(ClaimsPrincipal principal, UserId childId, SleepDiaryShareTokenId shareLinkId) =>
        new(principal.GetRequiredUserId(), childId, shareLinkId);
}
