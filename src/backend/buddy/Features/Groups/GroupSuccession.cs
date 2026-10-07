using buddy.Features.Guardians;
using buddy.Features.Users;

namespace buddy.Features.Groups;

// Who inherits a group when its owner's account is erased: the member who has belonged longest
// without a break, admins before members; children and deleted users never inherit. Null when nobody
// can, and the group is erased instead. Shared by the erasure (GroupsPersonalDataEraser) and the
// preview that shows it beforehand (GetAccountDeletionPreview). See gdpr-data-protection.md, Q4.
public static class GroupSuccession
{
    public static async Task<UserId?> FindSuccessorAsync(
        IReadOnlyCollection<GroupEvent> events,
        Group group,
        UserId leaving,
        IGuardianLinkEventStore guardians,
        IUserEventStore users,
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
