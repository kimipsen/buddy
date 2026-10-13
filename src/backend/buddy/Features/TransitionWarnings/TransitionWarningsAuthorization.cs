using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.TransitionWarnings;

// Same tier shape as ProgressAuthorization (docs/backend/analysis/transition-warnings.md,
// Question 8). Manage is any active guardian link, whatever its GuardianKind. The child reads their
// own settings only through /me, so on a /children/{childId} route the child is Forbidden: the
// guardian view names override items, which can include a sibling's task the child can't see.
public enum TransitionWarningsAccess
{
    Allowed,
    // No relationship to the child: a stranger can't tell "no such child" from "not your child".
    NotFound,
    // The child themself, on a guardian-only route.
    Forbidden
}

public static class TransitionWarningsAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this TransitionWarningsAccess access) => access switch
    {
        TransitionWarningsAccess.Forbidden => new Result<T>.Forbidden(),
        TransitionWarningsAccess.NotFound => new Result<T>.NotFound(),
        TransitionWarningsAccess.Allowed => throw new UnreachableException("ToDeniedResult called with TransitionWarningsAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized TransitionWarningsAccess value: {access}."),
    };
}

public static class TransitionWarningsAuthorization
{
    public static async Task<TransitionWarningsAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return TransitionWarningsAccess.Forbidden;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? TransitionWarningsAccess.Allowed : TransitionWarningsAccess.NotFound;
    }
}
