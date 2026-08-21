using buddy.Features.Groups;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListCalendarsHandler
{
    public static async Task<IReadOnlyCollection<CalendarMembershipDocument>> Handle(
        ListCalendars query, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return [];
        }

        var explicitMemberships = await calendars.ListForUserAsync(userId, cancellationToken);
        var groupMemberships = await groups.ListForUserAsync(userId, cancellationToken);

        if (groupMemberships.Count == 0)
        {
            return explicitMemberships;
        }

        var groupIds = groupMemberships.Select(m => new GroupId(m.GroupId)).Distinct().ToArray();
        var ownedCalendars = await calendars.ListOwnedByGroupsAsync(groupIds, cancellationToken);

        if (ownedCalendars.Count == 0)
        {
            return explicitMemberships;
        }

        // Explicit calendar membership always wins over a group-derived role, unconditionally --
        // see CalendarAuthorization.ResolveRole for the same precedence rule applied to
        // single-calendar authorization checks.
        var explicitCalendarIds = explicitMemberships.Select(m => m.CalendarId).ToHashSet();
        var roleByGroup = groupMemberships.ToDictionary(m => m.GroupId, m => m.Role);
        var groupCache = new Dictionary<Guid, Group?>();
        var derived = new List<CalendarMembershipDocument>();

        foreach (var owned in ownedCalendars)
        {
            if (explicitCalendarIds.Contains(owned.Id))
            {
                continue;
            }

            if (!groupCache.TryGetValue(owned.GroupId, out var group))
            {
                group = Group.Rehydrate(await groups.ReadAsync(new GroupId(owned.GroupId), cancellationToken));
                groupCache[owned.GroupId] = group;
            }

            // Same fail-closed rules as CalendarAuthorization.ResolveRole: a deleted group, a
            // revoked group role, or a policy missing an entry for the caller's role all mean "no
            // access", never a guessed default.
            if (group is null || group.IsDeleted)
            {
                continue;
            }

            if (!roleByGroup.TryGetValue(owned.GroupId, out var groupRole))
            {
                continue;
            }

            if (!group.CalendarPermissionPolicy.TryGetValue(groupRole, out var role))
            {
                continue;
            }

            derived.Add(new CalendarMembershipDocument(
                CalendarMembershipDocument.BuildId(owned.Id, userId.Value), owned.Id, userId.Value, role, owned.CalendarName));
        }

        return [.. explicitMemberships, .. derived];
    }
}
