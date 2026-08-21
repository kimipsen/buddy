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
