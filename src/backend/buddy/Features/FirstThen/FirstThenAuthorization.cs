using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.FirstThen;

// Two tiers (docs/backend/analysis/first-then-board.md, Question 5). Own: the child, on /me routes
// that can only ever reach the caller's board. Manage: any active guardian, on
// /children/{childId} routes. No group tier.
public enum FirstThenAccess
{
    Allowed,
    // No relationship at all -- a stranger can't tell "no such child" from "not your child". Also a
    // caller on /me who isn't a child with an active guardian.
    NotFound,
    // The child on a guardian-only route: the child knows their board exists, so 404 would protect
    // nothing.
    Forbidden
}

public static class FirstThenAuthorization
{
    // Same rule as FeelingsAuthorization.CheckSelf: the caller must be a child with at least one
    // active guardian link.
    public static async Task<FirstThenAccess> CheckOwn(UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken) =>
        (await guardians.ListForChildAsync(callerId, cancellationToken)).Count > 0
            ? FirstThenAccess.Allowed
            : FirstThenAccess.NotFound;

    public static async Task<FirstThenAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return FirstThenAccess.Forbidden;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? FirstThenAccess.Allowed : FirstThenAccess.NotFound;
    }
}

public static class FirstThenAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this FirstThenAccess access) => access switch
    {
        FirstThenAccess.Forbidden => new Result<T>.Forbidden(),
        FirstThenAccess.NotFound => new Result<T>.NotFound(),
        FirstThenAccess.Allowed => throw new UnreachableException("ToDeniedResult called with FirstThenAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized FirstThenAccess value: {access}."),
    };

    public static FirstThenWriteOutcome ToDeniedOutcome(this FirstThenAccess access) => access switch
    {
        FirstThenAccess.Forbidden => new FirstThenWriteOutcome.Forbidden(),
        FirstThenAccess.NotFound => new FirstThenWriteOutcome.NotFound(),
        FirstThenAccess.Allowed => throw new UnreachableException("ToDeniedOutcome called with FirstThenAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized FirstThenAccess value: {access}."),
    };
}
