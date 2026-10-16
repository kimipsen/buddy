using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record RevokeVisitSummaryShareLink(UserId UserId, UserId ChildId, VisitSummaryId SummaryId, VisitSummaryShareLinkId LinkId)
{
    public static RevokeVisitSummaryShareLink FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryId summaryId, VisitSummaryShareLinkId linkId) =>
        new(principal.GetRequiredUserId(), childId, summaryId, linkId);
}
