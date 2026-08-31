using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Progress;

// Mirrors MedicineAuthorization's tier shape. Unlike medicine doses, a child has no legitimate
// reason to write their own goal posts -- View is shared with the child themself (same as
// GetChildProgressHandler's original self-or-guardian check), but Manage (configuring goal
// posts) is guardian-only.
public enum ProgressAccessTier
{
    None,
    // The child themself, or a guardian: read the child's progress.
    View,
    // An active guardian only: everything View can do, plus configure goal posts.
    Manage
}

public enum ProgressAccess
{
    Allowed,
    // No relationship to the child at all -- collapsed the same way MedicineAccess.NotFound is,
    // so a stranger can't distinguish "no such child" from "not your child."
    NotFound,
    // The caller can View but the action needs Manage (e.g. the child tries to configure goals).
    Forbidden
}

public static class ProgressAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this ProgressAccess access) => access switch
    {
        ProgressAccess.Forbidden => new Result<T>.Forbidden(),
        ProgressAccess.NotFound => new Result<T>.NotFound(),
        ProgressAccess.Allowed => throw new UnreachableException("ToDeniedResult called with ProgressAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized ProgressAccess value: {access}."),
    };
}

public static class ProgressAuthorization
{
    public static async Task<ProgressAccess> CheckView(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == ProgressAccessTier.None ? ProgressAccess.NotFound : ProgressAccess.Allowed;
    }

    public static async Task<ProgressAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            ProgressAccessTier.Manage => ProgressAccess.Allowed,
            ProgressAccessTier.View => ProgressAccess.Forbidden,
            ProgressAccessTier.None => ProgressAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized ProgressAccessTier value: {tier}."),
        };
    }

    private static async Task<ProgressAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return ProgressAccessTier.View;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? ProgressAccessTier.Manage : ProgressAccessTier.None;
    }
}
