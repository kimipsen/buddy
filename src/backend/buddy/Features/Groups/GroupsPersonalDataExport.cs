using buddy.Common.Erasure;
using buddy.Features.Users;

using Marten;

namespace buddy.Features.Groups;

// The "groups" section: the groups the caller and their children belong to, with their role, and
// the group invites the caller sent or received. Other members aren't listed: they are other
// people's data. No invite token hashes.
public sealed class GroupsPersonalDataExporter(IGroupsStore store, IGroupEventStore groups) : IPersonalDataExporter
{
    public Type Store => typeof(IGroupsStore);

    public string Section => "groups";

    public async Task<object?> ExportAsync(ExportSubject subject, CancellationToken cancellationToken)
    {
        List<ExportedGroupMembership> memberships = [];

        foreach (var userId in (UserId[])[subject.UserId, .. subject.Children])
        {
            memberships.AddRange((await groups.ListForUserAsync(userId, cancellationToken))
                .Select(m => new ExportedGroupMembership(m.UserId, m.GroupId, m.GroupName, m.Role)));
        }

        var email = subject.Email is null ? null : GroupInviteDocument.NormalizeEmail(subject.Email);
        var caller = subject.UserId.Value;

        await using var session = store.QuerySession();
        var invites = await session.Query<GroupInviteDocument>()
            .Where(d => d.InvitedBy == caller || (email != null && d.InvitedEmail == email))
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return new GroupsExport(memberships, [.. invites.Select(ExportedGroupInvite.From)]);
    }
}

public sealed record GroupsExport(IReadOnlyList<ExportedGroupMembership> Memberships, IReadOnlyList<ExportedGroupInvite> Invites);

public sealed record ExportedGroupMembership(Guid UserId, Guid GroupId, string GroupName, GroupRole Role);

public sealed record ExportedGroupInvite(
    Guid Id,
    Guid GroupId,
    string GroupName,
    string InvitedEmail,
    GroupRole Role,
    Guid InvitedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    GroupInviteStatus Status)
{
    public static ExportedGroupInvite From(GroupInviteDocument d) =>
        new(d.Id, d.GroupId, d.GroupName, d.InvitedEmail, d.Role, d.InvitedBy, d.CreatedAt, d.ExpiresAt, d.Status);
}
