using System.Security.Claims;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

public sealed record CreateVisitSummary(UserId UserId, UserId ChildId, VisitSummaryDetails Details)
{
    public static CreateVisitSummary FromClaims(ClaimsPrincipal principal, UserId childId, VisitSummaryRequest request) =>
        new(principal.GetRequiredUserId(), childId, request.ToDetails());
}

// The body of both CreateVisitSummary and UpdateVisitSummary: the whole set of editable details.
public sealed record VisitSummaryRequest(
    string? Title,
    DateOnly? VisitDate,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<VisitSummarySection>? Sections,
    string? Note = null)
{
    public VisitSummaryDetails ToDetails() => new(
        FreeText.Normalize(Title),
        VisitDate,
        From,
        To,
        VisitSummaryDetails.Normalize(Sections ?? []),
        FreeText.Normalize(Note));
}
