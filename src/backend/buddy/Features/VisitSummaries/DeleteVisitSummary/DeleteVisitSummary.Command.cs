using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record DeleteVisitSummary(UserId UserId, UserId ChildId, VisitSummaryId SummaryId)
{
    public static DeleteVisitSummary FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryId summaryId) =>
        new(principal.GetRequiredUserId(), childId, summaryId);
}
