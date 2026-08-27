using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Groups;

public sealed record GroupMemberDetail(UserId UserId, Name Name, GroupRole Role, bool IsChild);

public sealed record GroupWithMemberDetails(Group Group, IReadOnlyCollection<GroupMemberDetail> Members);

// Resolves each member's name and child/guardian status -- the same pattern
// ListAssignableMembersHandler uses for calendar members: rehydrate each member's own User stream
// for their Name, and bulk-check which member ids are children via GuardianLinkDocument (a Group's
// Members dictionary carries only UserId + GroupRole, nothing about the account itself).
public static class GroupMemberResolver
{
    public static async Task<IReadOnlyCollection<GroupMemberDetail>> ResolveAsync(
        Group group,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
        CancellationToken cancellationToken)
    {
        var memberIds = group.Members.Keys.ToArray();
        var childIds = await guardians.FilterChildrenAsync(memberIds, cancellationToken);
        var childIdSet = new HashSet<UserId>(childIds);

        var members = new List<GroupMemberDetail>(memberIds.Length);

        foreach (var memberId in memberIds)
        {
            var userEvents = await users.ReadAsync(memberId, cancellationToken);

            if (User.Rehydrate(userEvents) is { IsDeleted: false } member)
            {
                members.Add(new GroupMemberDetail(member.Id, member.Name, group.Members[memberId], childIdSet.Contains(memberId)));
            }
        }

        return members;
    }
}
