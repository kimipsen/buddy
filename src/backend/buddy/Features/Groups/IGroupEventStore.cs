using buddy.Features.Users;

namespace buddy.Features.Groups;

public interface IGroupEventStore
{
    Task<IReadOnlyCollection<GroupEvent>> ReadAsync(GroupId groupId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupEvent>> CreateAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken);

    Task AppendAsync(GroupId groupId, IReadOnlyCollection<GroupEvent> events, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupMembershipDocument>> ListForUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<GroupInviteDocument>> ListPendingInvitesAsync(GroupId groupId, CancellationToken cancellationToken);

    Task<GroupInviteDocument?> FindInviteAsync(Guid inviteId, CancellationToken cancellationToken);

    Task<GroupInviteDocument?> FindPendingInviteAsync(GroupId groupId, string normalizedEmail, CancellationToken cancellationToken);

    Task<GroupInviteDocument?> FindInviteByTokenAsync(string token, CancellationToken cancellationToken);
}
