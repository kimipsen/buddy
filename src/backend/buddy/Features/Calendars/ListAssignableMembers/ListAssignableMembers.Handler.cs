using buddy.Common;
using buddy.Features.Groups;
using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Calendars;

// Who could reasonably be handed a task on this calendar: every explicit per-calendar grant, plus
// -- for a group-owned calendar -- every member of that group (regardless of their
// CalendarPermissionPolicy tier; a Viewer can still be asked to do a task even if they can't
// create one). Requires Contribute so only someone who could create a task can see the picker.
public static class ListAssignableMembersHandler
{
    public static async Task<Result<IReadOnlyCollection<AssignableMemberSummary>>> Handle(
        ListAssignableMembers query,
        ICalendarEventStore calendars,
        IGroupEventStore groups,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        if (query.UserId is not { } userId)
        {
            return new Result<IReadOnlyCollection<AssignableMemberSummary>>.NotFound();
        }

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);
        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, guardians, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<AssignableMemberSummary>>();
        }

        var memberIds = new HashSet<UserId>(calendar!.Members.Keys);

        if (calendar.Owner is CalendarOwner.Group(var groupId))
        {
            var group = Group.Rehydrate(await groups.ReadAsync(groupId, cancellationToken));

            if (group is not null && !group.IsDeleted)
            {
                memberIds.UnionWith(group.Members.Keys);
            }
        }
        else if (calendar.Owner is CalendarOwner.User(var ownerId))
        {
            memberIds.Add(ownerId);
        }

        var summaries = new List<AssignableMemberSummary>(memberIds.Count);

        foreach (var memberId in memberIds)
        {
            var userEvents = await users.ReadAsync(memberId, cancellationToken);

            if (User.Rehydrate(userEvents) is { IsDeleted: false } member)
            {
                summaries.Add(new AssignableMemberSummary(member.Id, member.Name));
            }
        }

        summaries.Sort((a, b) => string.CompareOrdinal(a.Name.GivenName, b.Name.GivenName));

        return new Result<IReadOnlyCollection<AssignableMemberSummary>>.Success(summaries);
    }
}

public sealed record AssignableMemberSummary(UserId Id, Name Name);
