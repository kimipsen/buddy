using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.VisitSummaries;

// Guardians only, like the sleep diary and effect notes it reads: an active guardian of the child
// manages every summary of that child, including a co-guardian's. The child and group members get
// nothing (docs/backend/analysis/doctor-visit-summary.md, Question 5).
public enum VisitSummaryAccess
{
    Allowed,
    // No guardian link -- including the child themself. "No such child" and "not your child" look
    // the same.
    NotFound
}

public static class VisitSummaryAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this VisitSummaryAccess access) => access switch
    {
        VisitSummaryAccess.NotFound => new Result<T>.NotFound(),
        VisitSummaryAccess.Allowed => throw new UnreachableException("ToDeniedResult called with VisitSummaryAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized VisitSummaryAccess value: {access}."),
    };
}

public static class VisitSummaryAuthorization
{
    public static async Task<VisitSummaryAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken) is not null
            ? VisitSummaryAccess.Allowed
            : VisitSummaryAccess.NotFound;
}
