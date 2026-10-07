using buddy.Common.Erasure;
using buddy.Features.Calendars;
using buddy.Features.Guardians;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Groups;

// Groups outlive any one member. An erased member's role is revoked. An erased owner hands the
// group to its longest-standing admin, else its longest-standing member -- adults only, never a child
// or a deleted user. A group nobody else is left in is erased: its calendars, its name and its
// invites masked, then deleted. Invites the person sent or received are revoked and their email
// masked. See gdpr-data-protection.md, Questions 2 and 4.
public sealed class GroupsPersonalDataEraser(
    IGroupsStore store,
    IGroupEventStore groups,
    ICalendarEventStore calendars,
    IGuardianLinkEventStore guardians,
    IUserEventStore users) : IPersonalDataEraser
{
    public Type Store => typeof(IGroupsStore);

    // Before Calendars: a group erased here takes its calendars with it.
    public int Order => -10;

    public static void ConfigureMasking(StoreOptions options)
    {
        options.Events.AddMaskingRuleForProtectedInformation<GroupCreated>(e => e with { Name = Erased.Text });
        options.Events.AddMaskingRuleForProtectedInformation<GroupInviteCreated>(e => e with { InvitedEmail = Erased.Text });
    }

    public async Task EraseGuardianAsync(ErasureSubject guardian, CancellationToken cancellationToken)
    {
        await LeaveGroupsAsync(guardian.UserId, cancellationToken);
        await EraseInvitesAsync(guardian, cancellationToken);
    }

    public Task EraseChildAsync(ErasureSubject child, UserId? heir, CancellationToken cancellationToken) =>
        LeaveGroupsAsync(child.UserId, cancellationToken);

    private async Task LeaveGroupsAsync(UserId userId, CancellationToken cancellationToken)
    {
        foreach (var membership in await groups.ListForUserAsync(userId, cancellationToken))
        {
            var groupId = new GroupId(membership.GroupId);
            var events = await groups.ReadAsync(groupId, cancellationToken);

            if (Group.Rehydrate(events) is not { IsDeleted: false } group || !group.Members.TryGetValue(userId, out var role))
            {
                continue;
            }

            var now = DateTimeOffset.UtcNow;

            if (role != GroupRole.Owner)
            {
                await groups.AppendAsync(groupId, [new GroupMemberRoleRevoked(groupId, userId, userId, now)], cancellationToken);
            }
            else if (await FindSuccessorAsync(events, group, userId, cancellationToken) is { } successor)
            {
                await groups.AppendAsync(
                    groupId,
                    [
                        new GroupMemberRoleGranted(groupId, successor, GroupRole.Owner, userId, now),
                        new GroupMemberRoleRevoked(groupId, userId, userId, now)
                    ],
                    cancellationToken);
            }
            else
            {
                await EraseGroupAsync(groupId, userId, cancellationToken);
            }
        }
    }

    // The order makes a rerun safe: calendars first (each erased before it's deleted, because
    // deleting removes the document that finds it), then the group's own stream and invites, and
    // GroupDeleted -- which removes the membership documents that lead here -- last.
    private async Task EraseGroupAsync(GroupId groupId, UserId erasedBy, CancellationToken cancellationToken)
    {
        foreach (var owned in await calendars.ListOwnedByGroupsAsync([groupId], cancellationToken))
        {
            var calendarId = new CalendarId(owned.Id);
            await calendars.EraseAsync(calendarId, cancellationToken);
            await calendars.AppendAsync(calendarId, [new CalendarDeleted(calendarId, erasedBy, DateTimeOffset.UtcNow)], cancellationToken);
        }

        await store.MaskStreamAsync<GroupSnapshot>(groupId.Value, cancellationToken);
        await RewriteInvitesAsync(d => d.GroupId == groupId.Value, eraseGroupName: true, cancellationToken);

        await groups.AppendAsync(groupId, [new GroupDeleted(groupId, erasedBy, DateTimeOffset.UtcNow)], cancellationToken);
    }

    private async Task EraseInvitesAsync(ErasureSubject person, CancellationToken cancellationToken)
    {
        var email = person.Email is null ? null : GroupInviteDocument.NormalizeEmail(person.Email);
        var userId = person.UserId.Value;

        IReadOnlyList<GroupInviteDocument> invites;

        await using (var session = store.QuerySession())
        {
            invites = await session.Query<GroupInviteDocument>()
                .Where(d => d.InvitedEmail != Erased.Text && (d.InvitedBy == userId || (email != null && d.InvitedEmail == email)))
                .ToListAsync(cancellationToken);
        }

        foreach (var byGroup in invites.GroupBy(i => i.GroupId))
        {
            var groupId = new GroupId(byGroup.Key);
            var inviteIds = byGroup.Select(i => i.Id).ToHashSet();

            GroupEvent[] revocations =
            [
                .. byGroup
                    .Where(i => i.Status == GroupInviteStatus.Pending)
                    .Select(i => (GroupEvent)new GroupInviteRevoked(groupId, i.Id, person.UserId, DateTimeOffset.UtcNow))
            ];
            await groups.AppendAsync(groupId, revocations, cancellationToken);

            await store.MaskStreamAsync<GroupSnapshot>(
                groupId.Value,
                cancellationToken,
                e => e.Data is GroupInviteCreated created && inviteIds.Contains(created.InviteId));
        }

        var ids = invites.Select(i => i.Id).ToArray();
        await RewriteInvitesAsync(d => ids.Contains(d.Id), eraseGroupName: false, cancellationToken);
    }

    private async Task RewriteInvitesAsync(
        System.Linq.Expressions.Expression<Func<GroupInviteDocument, bool>> which,
        bool eraseGroupName,
        CancellationToken cancellationToken)
    {
        await using var session = store.LightweightSession();

        foreach (var invite in await session.Query<GroupInviteDocument>().Where(which).ToListAsync(cancellationToken))
        {
            session.Store(invite with
            {
                InvitedEmail = Erased.Text,
                GroupName = eraseGroupName ? Erased.Text : invite.GroupName,
            });
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    // The member who has belonged longest without a break, admins before members; children and
    // deleted users never inherit a group.
    private async Task<UserId?> FindSuccessorAsync(
        IReadOnlyCollection<GroupEvent> events,
        Group group,
        UserId leaving,
        CancellationToken cancellationToken)
    {
        var joinedAt = new Dictionary<UserId, DateTimeOffset>();

        foreach (var @event in events)
        {
            switch (@event)
            {
                case GroupCreated created:
                    joinedAt[created.OwnerId] = created.OccurredAt;
                    break;
                case GroupMemberRoleGranted granted:
                    joinedAt.TryAdd(granted.MemberId, granted.OccurredAt);
                    break;
                case GroupMemberRoleRevoked revoked:
                    joinedAt.Remove(revoked.MemberId);
                    break;
            }
        }

        var others = group.Members.Keys.Where(id => id != leaving).ToArray();
        var children = (await guardians.FilterChildrenAsync(others, cancellationToken)).ToHashSet();

        var ordered = others
            .Where(id => !children.Contains(id))
            .OrderBy(id => group.Members[id] == GroupRole.Admin ? 0 : 1)
            .ThenBy(id => joinedAt.GetValueOrDefault(id, DateTimeOffset.MaxValue));

        foreach (var candidate in ordered)
        {
            if (await users.FindSnapshotAsync(candidate, cancellationToken) is { IsDeleted: false })
            {
                return candidate;
            }
        }

        return null;
    }
}
