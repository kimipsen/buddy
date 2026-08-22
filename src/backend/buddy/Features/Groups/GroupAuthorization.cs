using System.Diagnostics;

using buddy.Common;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public enum GroupAccess
{
    Allowed,
    // The group doesn't exist, is deleted, or the caller isn't a member -- collapsed into one
    // outcome so a non-member can't distinguish a private group from a missing one.
    NotFound,
    // The caller can see the group but lacks the permission tier the operation requires.
    Forbidden
}

public static class GroupAccessExtensions
{
    // Maps a denied GroupAccess to the matching Result<T> failure. Callers must already know
    // access isn't Allowed -- see every CheckView/CheckManage/CheckOwner call site.
    public static Result<T> ToDeniedResult<T>(this GroupAccess access) => access switch
    {
        GroupAccess.Forbidden => new Result<T>.Forbidden(),
        GroupAccess.NotFound => new Result<T>.NotFound(),
        GroupAccess.Allowed => throw new UnreachableException("ToDeniedResult called with GroupAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized GroupAccess value: {access}."),
    };
}

public static class GroupAuthorization
{
    public static GroupAccess CheckView(Group? group, UserId userId) =>
        group is not null && CanView(group, userId) ? GroupAccess.Allowed : GroupAccess.NotFound;

    public static GroupAccess CheckManage(Group? group, UserId userId)
    {
        if (group is null || !CanView(group, userId))
        {
            return GroupAccess.NotFound;
        }

        return CanManage(group, userId) ? GroupAccess.Allowed : GroupAccess.Forbidden;
    }

    public static GroupAccess CheckOwner(Group? group, UserId userId)
    {
        if (group is null || !CanView(group, userId))
        {
            return GroupAccess.NotFound;
        }

        return IsOwner(group, userId) ? GroupAccess.Allowed : GroupAccess.Forbidden;
    }

    private static bool CanView(Group group, UserId userId) => !group.IsDeleted && group.Members.ContainsKey(userId);

    private static bool CanManage(Group group, UserId userId) =>
        !group.IsDeleted && group.Members.TryGetValue(userId, out var role) && role is GroupRole.Owner or GroupRole.Admin;

    private static bool IsOwner(Group group, UserId userId) =>
        !group.IsDeleted && group.Members.TryGetValue(userId, out var role) && role == GroupRole.Owner;
}
