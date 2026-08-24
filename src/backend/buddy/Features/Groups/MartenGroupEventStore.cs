using buddy.Features.Users;

using Marten;

namespace buddy.Features.Groups;

public sealed class MartenGroupEventStore(IGroupsStore store) : IGroupEventStore
{
    public async Task<IReadOnlyCollection<GroupEvent>> ReadAsync(GroupId groupId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var events = await session.Events.FetchStreamAsync(groupId.Value, token: cancellationToken);

        return [.. events.Select(e => GroupEvent.FromPayload(e.Data))];
    }

    public async Task<IReadOnlyCollection<GroupEvent>> CreateAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken)
    {
        if (events.FirstOrDefault() is not GroupCreated created)
        {
            throw new InvalidOperationException("The first event of a new group stream must be GroupCreated.");
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty group event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.StartStream(groupId.Value, payloads);
        session.Store(new GroupMembershipDocument(
            GroupMembershipDocument.BuildId(groupId.Value, created.OwnerId.Value),
            groupId.Value,
            created.OwnerId.Value,
            GroupRole.Owner,
            created.Name));

        await session.SaveChangesAsync(cancellationToken);

        return events;
    }

    public async Task AppendAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return;
        }

        var payloads = events
            .Select(e => e.Value ?? throw new InvalidOperationException("Cannot persist an empty group event."))
            .ToArray();

        await using var session = store.LightweightSession();
        session.Events.Append(groupId.Value, payloads);

        foreach (var @event in events)
        {
            switch (@event)
            {
                case GroupMemberRoleGranted granted:
                    var name = await session.Query<GroupMembershipDocument>()
                        .Where(d => d.GroupId == groupId.Value)
                        .Select(d => d.GroupName)
                        .FirstAsync(cancellationToken);

                    session.Store(new GroupMembershipDocument(
                        GroupMembershipDocument.BuildId(groupId.Value, granted.MemberId.Value),
                        groupId.Value,
                        granted.MemberId.Value,
                        granted.Role,
                        name));
                    break;

                case GroupMemberRoleRevoked revoked:
                    session.Delete<GroupMembershipDocument>(GroupMembershipDocument.BuildId(groupId.Value, revoked.MemberId.Value));
                    break;

                case GroupDeleted:
                    var members = await session.Query<GroupMembershipDocument>()
                        .Where(d => d.GroupId == groupId.Value)
                        .ToListAsync(cancellationToken);

                    foreach (var member in members)
                    {
                        session.Delete(member);
                    }
                    break;

                case GroupInviteCreated created:
                    var groupName = await session.Query<GroupMembershipDocument>()
                        .Where(d => d.GroupId == groupId.Value)
                        .Select(d => d.GroupName)
                        .FirstAsync(cancellationToken);

                    session.Store(new GroupInviteDocument(
                        created.InviteId,
                        groupId.Value,
                        groupName,
                        created.InvitedEmail,
                        created.Role,
                        created.InvitedBy.Value,
                        created.TokenHash,
                        created.OccurredAt,
                        created.ExpiresAt,
                        GroupInviteStatus.Pending));
                    break;

                case GroupInviteAccepted accepted:
                    var acceptedInvite = await session.LoadAsync<GroupInviteDocument>(accepted.InviteId, cancellationToken);
                    if (acceptedInvite is not null)
                    {
                        session.Store(acceptedInvite with { Status = GroupInviteStatus.Accepted });
                    }
                    break;

                case GroupInviteRevoked revoked:
                    var revokedInvite = await session.LoadAsync<GroupInviteDocument>(revoked.InviteId, cancellationToken);
                    if (revokedInvite is not null)
                    {
                        session.Store(revokedInvite with { Status = GroupInviteStatus.Revoked });
                    }
                    break;
            }
        }

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<GroupMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GroupMembershipDocument>()
            .Where(d => d.UserId == userId.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<GroupInviteDocument>> ListPendingInvitesAsync(GroupId groupId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GroupInviteDocument>()
            .Where(d => d.GroupId == groupId.Value && d.Status == GroupInviteStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<GroupInviteDocument?> FindInviteAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.LoadAsync<GroupInviteDocument>(inviteId, cancellationToken);
    }

    public async Task<GroupInviteDocument?> FindPendingInviteAsync(GroupId groupId, string normalizedEmail, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();

        return await session.Query<GroupInviteDocument>()
            .Where(d => d.GroupId == groupId.Value && d.InvitedEmail == normalizedEmail && d.Status == GroupInviteStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GroupInviteDocument?> FindInviteByTokenAsync(string token, CancellationToken cancellationToken)
    {
        await using var session = store.QuerySession();
        var tokenHash = GroupInviteToken.Hash(token);

        return await session.Query<GroupInviteDocument>()
            .Where(d => d.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
