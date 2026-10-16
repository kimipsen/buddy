using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record UpdateVisitSummary(UserId UserId, UserId ChildId, VisitSummaryId SummaryId, VisitSummaryDetails Details)
{
    public static UpdateVisitSummary FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryId summaryId, VisitSummaryRequest request) =>
        new(principal.GetRequiredUserId(), childId, summaryId, request.ToDetails());
}
