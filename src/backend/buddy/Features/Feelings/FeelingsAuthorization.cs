using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Feelings;

// Two tiers (docs/backend/analysis/feelings-check-in.md, Question 5). Self: the child, on /me
// routes that can only ever reach their own journal. Manage: any active guardian, on
// /children/{childId} routes. No group tier.
public enum FeelingsAccess
{
    Allowed,
    // No relationship at all -- a stranger can't tell "no such child" from "not your child". Also a
    // caller on /me who isn't a child with an active guardian (a guardian would otherwise start a
    // journal about themselves).
    NotFound,
    // The child on a guardian-only route: the child knows their journal exists, so 404 would
    // protect nothing.
    Forbidden
}

public static class FeelingsAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this FeelingsAccess access) => access switch
    {
        FeelingsAccess.Forbidden => new Result<T>.Forbidden(),
        FeelingsAccess.NotFound => new Result<T>.NotFound(),
        FeelingsAccess.Allowed => throw new UnreachableException("ToDeniedResult called with FeelingsAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized FeelingsAccess value: {access}."),
    };
}

public static class FeelingsAuthorization
{
    // The caller checks in about themself, so they must be a child with at least one active guardian
    // link (ChildVisibility's rule). A child whose last link was revoked drops to NotFound: nobody
    // could read what they wrote.
    public static async Task<FeelingsAccess> CheckSelf(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        (await guardians.ListForChildAsync(callerId, cancellationToken)).Count > 0
            ? FeelingsAccess.Allowed
            : FeelingsAccess.NotFound;

    public static async Task<FeelingsAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return FeelingsAccess.Forbidden;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? FeelingsAccess.Allowed : FeelingsAccess.NotFound;
    }
}
