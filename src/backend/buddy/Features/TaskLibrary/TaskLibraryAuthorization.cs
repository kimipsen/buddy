using System.Diagnostics;

using buddy.Common;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.TaskLibrary;

// Simpler two-tier axis than MealplanAccessTier's three: there's no separate "rate"-style action
// only the child can take here, and (for this step -- see docs deviation notes) no group-sharing
// axis either. Manage is a guardian-only concern; View is granted to both an active guardian and
// the child themself, mirroring MealplanAccessTier.Rate's "the child can see their own library"
// contract even though there's no analogous write-only-the-child-can-do action to name it after.
public enum TaskLibraryAccessTier
{
    None,
    // An active guardian, or the child themself: view the task library.
    View,
    // An active guardian only: create/edit/archive templates and their subtasks.
    Manage
}

public enum TaskLibraryAccess
{
    Allowed,
    // No relationship to the child at all -- collapsed the same way MealplanAccess.NotFound is.
    NotFound,
    // The caller has some access but not the tier the action needs.
    Forbidden
}

public static class TaskLibraryAccessExtensions
{
    public static Result<T> ToDeniedResult<T>(this TaskLibraryAccess access) => access switch
    {
        TaskLibraryAccess.Forbidden => new Result<T>.Forbidden(),
        TaskLibraryAccess.NotFound => new Result<T>.NotFound(),
        TaskLibraryAccess.Allowed => throw new UnreachableException("ToDeniedResult called with TaskLibraryAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized TaskLibraryAccess value: {access}."),
    };
}

public static class TaskLibraryAuthorization
{
    public static async Task<TaskLibraryAccess> CheckView(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier == TaskLibraryAccessTier.None ? TaskLibraryAccess.NotFound : TaskLibraryAccess.Allowed;
    }

    public static async Task<TaskLibraryAccess> CheckManage(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var tier = await ResolveTier(childId, callerId, guardians, cancellationToken);

        return tier switch
        {
            TaskLibraryAccessTier.Manage => TaskLibraryAccess.Allowed,
            TaskLibraryAccessTier.View => TaskLibraryAccess.Forbidden,
            TaskLibraryAccessTier.None => TaskLibraryAccess.NotFound,
            _ => throw new UnreachableException($"Unrecognized TaskLibraryAccessTier value: {tier}."),
        };
    }

    private static async Task<TaskLibraryAccessTier> ResolveTier(UserId childId, UserId callerId, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (callerId == childId)
        {
            return TaskLibraryAccessTier.View;
        }

        var link = await guardians.FindActiveLinkAsync(childId, callerId, cancellationToken);

        return link is not null ? TaskLibraryAccessTier.Manage : TaskLibraryAccessTier.None;
    }
}
