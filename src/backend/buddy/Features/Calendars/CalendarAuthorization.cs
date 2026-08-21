using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public enum CalendarAccess
{
    Allowed,
    // The calendar doesn't exist, is deleted, or the caller isn't a member -- collapsed into one
    // outcome so a non-member can't distinguish a private calendar from a missing one.
    NotFound,
    // The caller can see the calendar but lacks the permission tier the operation requires.
    Forbidden
}

// Resolves a user's effective CalendarRole on a calendar. For a user-owned calendar this only
// ever reads Calendar.Members (no group lookup, no extra cost). For a group-owned calendar, Group
// is loaded (mirroring how Calendar itself is rehydrated per request, no caching layer) only when
// the caller has no explicit Calendar.Members entry -- see
// docs/backend/analysis/group-owned-calendars-and-permissions.md for the full resolution contract.
public static class CalendarAuthorization
{
    public static async Task<CalendarAccess> CheckView(Calendar? calendar, UserId userId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, cancellationToken);

        return role is not null ? CalendarAccess.Allowed : CalendarAccess.NotFound;
    }

    public static async Task<CalendarAccess> CheckContribute(Calendar? calendar, UserId userId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, cancellationToken);

        if (role is null)
        {
            return CalendarAccess.NotFound;
        }

        return role is CalendarRole.Owner or CalendarRole.Contributor ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }

    public static async Task<CalendarAccess> CheckOwner(Calendar? calendar, UserId userId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, cancellationToken);

        if (role is null)
        {
            return CalendarAccess.NotFound;
        }

        return role == CalendarRole.Owner ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }

    private static async Task<CalendarRole?> ResolveRole(Calendar calendar, UserId userId, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (calendar.IsDeleted)
        {
            return null;
        }

        // Explicit per-calendar grants always win, unconditionally -- even over a higher-privilege
        // group-derived role.
        if (calendar.Members.TryGetValue(userId, out var explicitRole))
        {
            return explicitRole;
        }

        if (calendar.Owner is CalendarOwner.Group(var groupId))
        {
            var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

            // TryGetValue, never GetValueOrDefault: CalendarRole's default value is Owner (enum
            // case 0), so defaulting a missing policy entry would fail *open*. A missing entry
            // must fail closed -- treated the same as not being a group member at all.
            if (group is not null && !group.IsDeleted
                && group.Members.TryGetValue(userId, out var groupRole)
                && group.CalendarPermissionPolicy.TryGetValue(groupRole, out var mappedRole))
            {
                return mappedRole;
            }
        }

        return null;
    }
}
