using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.WorkLocations;

public enum WorkLocationAccess
{
    Allowed,
    // No relationship at all -- collapsed to 404 like every other feature, so a caller can't tell
    // "not a co-guardian" from "no such user".
    NotFound,
    // A child account on a write route: related to guardians, but children have no work locations.
    Forbidden
}

public static class WorkLocationAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this WorkLocationAccess access) => access switch
    {
        WorkLocationAccess.Forbidden => new Result<T>.Forbidden(),
        WorkLocationAccess.NotFound => new Result<T>.NotFound(),
        WorkLocationAccess.Allowed => throw new UnreachableException("ToDeniedResult called with WorkLocationAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized WorkLocationAccess value: {access}."),
    };
}

// Manage = the guardian themself (every write route is a /me route, so there is no guardian id to
// check). View = the guardian or a co-guardian: someone holding an active GuardianLink to at least
// one of the same children. Children never get View -- a child account holds no guardian links of
// its own, so it can't pass the intersection. See docs/backend/analysis/work-locations.md, Question 6.
public static class WorkLocationAuthorization
{
    public static async Task<WorkLocationAccess> CheckManage(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var linksAsChild = await guardians.ListForChildAsync(callerId, cancellationToken);

        return linksAsChild.Count == 0 ? WorkLocationAccess.Allowed : WorkLocationAccess.Forbidden;
    }

    public static async Task<WorkLocationAccess> CheckView(UserId guardianId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (guardianId == callerId)
        {
            return await CheckManage(callerId, guardians, cancellationToken) == WorkLocationAccess.Allowed
                ? WorkLocationAccess.Allowed
                : WorkLocationAccess.NotFound;
        }

        return await AreCoGuardiansAsync(guardianId, callerId, guardians, cancellationToken)
            ? WorkLocationAccess.Allowed
            : WorkLocationAccess.NotFound;
    }

    public static async Task<bool> AreCoGuardiansAsync(UserId first, UserId second, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var firstChildren = (await guardians.ListForGuardianAsync(first, cancellationToken))
            .Select(link => link.ChildId)
            .ToHashSet();

        if (firstChildren.Count == 0)
        {
            return false;
        }

        var secondLinks = await guardians.ListForGuardianAsync(second, cancellationToken);

        return secondLinks.Any(link => firstChildren.Contains(link.ChildId));
    }
}
