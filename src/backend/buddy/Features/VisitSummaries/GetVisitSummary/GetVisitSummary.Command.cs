using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record GetVisitSummary(UserId UserId, UserId ChildId, VisitSummaryId SummaryId)
{
    public static GetVisitSummary FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryId summaryId) =>
        new(principal.GetRequiredUserId(), childId, summaryId);
}
