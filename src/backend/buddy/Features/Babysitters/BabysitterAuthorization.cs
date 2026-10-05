using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Babysitters;

public enum BabysitterAccess
{
    Allowed,
    // No relationship at all -- collapsed to 404 like every other feature.
    NotFound,
    // A child account on a /me route: related to guardians, but children keep no babysitters.
    Forbidden
}

public static class BabysitterAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this BabysitterAccess access) => access switch
    {
        BabysitterAccess.Forbidden => new Result<T>.Forbidden(),
        BabysitterAccess.NotFound => new Result<T>.NotFound(),
        BabysitterAccess.Allowed => throw new UnreachableException("ToDeniedResult called with BabysitterAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized BabysitterAccess value: {access}."),
    };
}

// Manage = the guardian themself (every write route is a /me route), same as
// WorkLocationAuthorization.CheckManage. Pick = an active guardian of the child, the same rule
// PickupAuthorization.CheckManage applies to writing that child's pickups, since the per-child list
// only exists to fill the pickup planner's dropdown. See docs/backend/analysis/babysitters.md, Question 5.
public static class BabysitterAuthorization
{
    public static async Task<BabysitterAccess> CheckManage(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var linksAsChild = await guardians.ListForChildAsync(callerId, cancellationToken);

        return linksAsChild.Count == 0 ? BabysitterAccess.Allowed : BabysitterAccess.Forbidden;
    }

    public static async Task<BabysitterAccess> CheckPick(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken) is not null
            ? BabysitterAccess.Allowed
            : BabysitterAccess.NotFound;
}
