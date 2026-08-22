using System.Diagnostics;

using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
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

public static class CalendarAccessExtensions
{
    // Maps a denied CalendarAccess to the matching Result<T> failure. Callers must already know
    // access isn't Allowed -- see every CheckView/CheckContribute/CheckOwner call site.
    public static Result<T> ToDeniedResult<T>(this CalendarAccess access) => access switch
    {
        CalendarAccess.Forbidden => new Result<T>.Forbidden(),
        CalendarAccess.NotFound => new Result<T>.NotFound(),
        CalendarAccess.Allowed => throw new UnreachableException("ToDeniedResult called with CalendarAccess.Allowed."),
        _ => throw new UnreachableException($"Unrecognized CalendarAccess value: {access}."),
    };
}

// Resolves a user's effective CalendarRole on a calendar. For a user-owned calendar this only
// ever reads Calendar.Members (no group lookup, no extra cost) unless the caller turns out to be
// the owner's guardian (see the third step below). For a group-owned calendar, Group is loaded
// (mirroring how Calendar itself is rehydrated per request, no caching layer) only when the
// caller has no explicit Calendar.Members entry -- see
// docs/backend/analysis/group-owned-calendars-and-permissions.md for the full resolution contract.
public static class CalendarAuthorization
{
    public static async Task<CalendarAccess> CheckView(Calendar? calendar, UserId userId, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, guardians, cancellationToken);

        return role is not null ? CalendarAccess.Allowed : CalendarAccess.NotFound;
    }

    public static async Task<CalendarAccess> CheckContribute(Calendar? calendar, UserId userId, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, guardians, cancellationToken);

        if (role is null)
        {
            return CalendarAccess.NotFound;
        }

        return role is CalendarRole.Owner or CalendarRole.Contributor ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }

    public static async Task<CalendarAccess> CheckOwner(Calendar? calendar, UserId userId, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (calendar is null)
        {
            return CalendarAccess.NotFound;
        }

        var role = await ResolveRole(calendar, userId, groups, guardians, cancellationToken);

        if (role is null)
        {
            return CalendarAccess.NotFound;
        }

        return role == CalendarRole.Owner ? CalendarAccess.Allowed : CalendarAccess.Forbidden;
    }

    private static async Task<CalendarRole?> ResolveRole(Calendar calendar, UserId userId, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (calendar.IsDeleted)
        {
            return null;
        }

        // Explicit per-calendar grants always win, unconditionally -- even over a higher-privilege
        // group- or guardian-derived role.
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
        // The owner's own access is already covered by the explicit-Members check above (seeded at
        // CalendarCreated), so this only fires for a caller who isn't the owner and has no explicit
        // grant -- exactly the guardian case. Not configurable per child, unlike
        // CalendarPermissionPolicy: a guardian's authority over a dependent's account is a
        // safety/parental-control property, not something anyone should downgrade by policy.
        else if (calendar.Owner is CalendarOwner.User(var ownerId) && ownerId != userId)
        {
            var link = await guardians.FindActiveLinkAsync(ownerId, userId, cancellationToken);

            if (link is not null)
            {
                return CalendarRole.Owner;
            }
        }

        return null;
    }
}
