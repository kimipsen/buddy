using buddy.Common;
using buddy.Features.Groups;
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
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var userId = query.UserId;

        var calendarEvents = await calendars.ReadAsync(query.CalendarId, cancellationToken);
        var calendar = Calendar.Rehydrate(calendarEvents);

        if (calendar is null)
        {
            return new Result<IReadOnlyCollection<AssignableMemberSummary>>.NotFound();
        }

        var access = await CalendarAuthorization.CheckContribute(calendar, userId, groups, cancellationToken);

        if (access != CalendarAccess.Allowed)
        {
            return access.ToDeniedResult<IReadOnlyCollection<AssignableMemberSummary>>();
        }

        var memberIds = new HashSet<UserId>(calendar.Members.Keys);

        var group = Group.Rehydrate(await groups.ReadAsync(calendar.GroupId, cancellationToken));

        if (group is not null && !group.IsDeleted)
        {
            memberIds.UnionWith(group.Members.Keys);
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
