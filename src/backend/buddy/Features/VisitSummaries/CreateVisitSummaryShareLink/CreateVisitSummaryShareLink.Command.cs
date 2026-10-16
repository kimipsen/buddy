using System.Security.Claims;

using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record CreateVisitSummaryShareLink(UserId UserId, UserId ChildId, VisitSummaryId SummaryId, DateTimeOffset? ExpiresAt)
{
    public static CreateVisitSummaryShareLink FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryId summaryId, DateTimeOffset? expiresAt) =>
        new(principal.GetRequiredUserId(), childId, summaryId, expiresAt);
}
