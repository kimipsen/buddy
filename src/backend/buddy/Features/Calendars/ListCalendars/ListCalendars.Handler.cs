using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

public static class ListCalendarsHandler
{
    public static async Task<IReadOnlyCollection<CalendarMembershipDocument>> Handle(
        ListCalendars query, ICalendarEventStore calendars, IGroupEventStore groups, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return [];
        }

        var explicitMemberships = await calendars.ListForUserAsync(userId, cancellationToken);
        var explicitCalendarIds = explicitMemberships.Select(m => m.CalendarId).ToHashSet();

        var groupDerived = await ResolveGroupDerivedAsync(userId, explicitCalendarIds, calendars, groups, cancellationToken);
        var guardianDerived = await ResolveGuardianDerivedAsync(userId, explicitCalendarIds, calendars, guardians, cancellationToken);

        return [.. explicitMemberships, .. groupDerived, .. guardianDerived];
    }

    private static async Task<IReadOnlyCollection<CalendarMembershipDocument>> ResolveGroupDerivedAsync(
        UserId userId, HashSet<Guid> explicitCalendarIds, ICalendarEventStore calendars, IGroupEventStore groups, CancellationToken cancellationToken)
    {
        var groupMemberships = await groups.ListForUserAsync(userId, cancellationToken);

        if (groupMemberships.Count == 0)
        {
            return [];
        }

        var groupIds = groupMemberships.Select(m => new GroupId(m.GroupId)).Distinct().ToArray();
        var ownedCalendars = await calendars.ListOwnedByGroupsAsync(groupIds, cancellationToken);

        if (ownedCalendars.Count == 0)
        {
            return [];
        }

        // Explicit calendar membership always wins over a group-derived role, unconditionally --
        // see CalendarAuthorization.ResolveRole for the same precedence rule applied to
        // single-calendar authorization checks.
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
                CalendarMembershipDocument.BuildId(owned.Id, userId.Value), owned.Id, userId.Value, role, owned.CalendarName, owned.Icon));
        }

        return derived;
    }

    // Lowest precedence, same as CalendarAuthorization.ResolveRole's guardian step: a linked
    // child's own calendars already carry an Owner CalendarMembershipDocument keyed to the CHILD's
    // UserId, so this rewrites the guardian's own UserId into the response entries -- the response
    // DTO (CalendarSummaryResponse) never exposes UserId, so this is purely an internal shape fix,
    // not a claim about who the document's row "really" belongs to.
    private static async Task<IReadOnlyCollection<CalendarMembershipDocument>> ResolveGuardianDerivedAsync(
        UserId userId, HashSet<Guid> explicitCalendarIds, ICalendarEventStore calendars, IGuardianLinkEventStore guardians, CancellationToken cancellationToken)
    {
        var links = await guardians.ListForGuardianAsync(userId, cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var childIds = links.Select(l => new UserId(l.ChildId)).Distinct().ToArray();
        var ownedCalendars = await calendars.ListOwnedByUsersAsync(childIds, cancellationToken);

        return [.. ownedCalendars
            .Where(owned => !explicitCalendarIds.Contains(owned.CalendarId))
            .Select(owned => owned with { Id = CalendarMembershipDocument.BuildId(owned.CalendarId, userId.Value), UserId = userId.Value })];
    }
}
